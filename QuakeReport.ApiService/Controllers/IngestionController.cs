using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Ingestion;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;
using QuakeReport.Data.Geospatial;

namespace QuakeReport.ApiService.Controllers;

[ApiController]
[Route("api/ingestion/v1")]
[EnableRateLimiting("ingestion")]
[RequestSizeLimit(256_000)]
public sealed class IngestionController(
    QuakeReportDbContext db,
    ActiveEarthquakeService earthquakes,
    IIngestionApiKeyValidator apiKey) : ControllerBase
{
    private const int MaxPageText = 4000;

    [HttpPost("collection-points")]
    public Task<IActionResult> CollectionPoint(IngestionCollectionPointRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(IngestionEntityType.CollectionPoint, request.Source, request.Data, CreateCollectionPoint, cancellationToken);

    [HttpPost("blood-donation-centers")]
    public Task<IActionResult> BloodDonationCenter(IngestionBloodDonationCenterRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(IngestionEntityType.BloodDonationCenter, request.Source, request.Data, CreateBloodDonationCenter, cancellationToken);

    [HttpPost("shelters")]
    public Task<IActionResult> Shelter(IngestionShelterRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(IngestionEntityType.Shelter, request.Source, request.Data, CreateShelter, cancellationToken);

    [HttpPost("help-requests")]
    public Task<IActionResult> HelpRequest(IngestionHelpRequestRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(IngestionEntityType.HelpRequest, request.Source, request.Data, CreateHelpRequest, cancellationToken);

    private async Task<IActionResult> ExecuteAsync<T>(
        IngestionEntityType entityType,
        IngestionSource source,
        T data,
        Func<T, Guid, Guid, (Guid id, string? error)> create,
        CancellationToken cancellationToken)
    {
        if (!apiKey.IsValid(Request.Headers["X-Ingestion-Api-Key"].FirstOrDefault())) return Unauthorized();
        if (!ValidateSource(source, out var sourceError)) return BadRequest(sourceError);
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var keyValues) || string.IsNullOrWhiteSpace(keyValues.FirstOrDefault()))
            return BadRequest("Idempotency-Key is required.");

        var idempotencyHash = Hash(keyValues.First()!.Trim());
        var duplicate = await db.IngestionSubmissions.SingleOrDefaultAsync(
            item => item.EntityType == entityType && item.IdempotencyKeyHash == idempotencyHash, cancellationToken);
        if (duplicate is not null)
            return Ok(new IngestionSubmissionResponse(duplicate.Id, duplicate.EntityId, duplicate.EntityType, "Pending", true, PublicPath(duplicate.EntityType, duplicate.EntityId)));

        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null) return UnprocessableEntity("No active earthquake is configured.");

        var created = create(data, earthquake.Id, Guid.NewGuid());
        if (created.error is not null) return BadRequest(created.error);

        var externalDuplicate = source.ExternalPostId is null ? null : await db.IngestionSubmissions.SingleOrDefaultAsync(
            item => item.EntityType == entityType && item.Platform == source.Platform && item.ExternalPostId == source.ExternalPostId, cancellationToken);
        if (externalDuplicate is not null)
            return Conflict(new IngestionSubmissionResponse(externalDuplicate.Id, externalDuplicate.EntityId, externalDuplicate.EntityType, "Pending", true, PublicPath(externalDuplicate.EntityType, externalDuplicate.EntityId)));

        var submission = new IngestionSubmission
        {
            Id = Guid.NewGuid(), EarthquakeId = earthquake.Id, EntityType = entityType, EntityId = created.id,
            Platform = source.Platform, SourceUrl = source.SourceUrl.Trim(), ExternalPostId = source.ExternalPostId?.Trim(),
            IdempotencyKeyHash = idempotencyHash, PublishedAt = source.PublishedAt,
            ExtractedAt = source.ExtractedAt ?? DateTimeOffset.UtcNow, Confidence = source.Confidence,
            EvidenceSummary = source.EvidenceSummary?.Trim()
        };
        db.IngestionSubmissions.Add(submission);
        await db.SaveChangesAsync(cancellationToken);
        return Created(PublicPath(entityType, created.id), new IngestionSubmissionResponse(submission.Id, created.id, entityType, "Pending", false, PublicPath(entityType, created.id)));
    }

    private (Guid id, string? error) CreateCollectionPoint(IngestionCollectionPointData data, Guid earthquakeId, Guid id)
    {
        var error = ValidateCommon(data.Name, data.Address, data.Description, data.NeedsSummary, data.ReceivingInstructions);
        if (error is not null) return (id, error);
        if (!ValidCoordinates(data.Latitude, data.Longitude)) return (id, "Coordinates must be supplied together and be valid.");
        var point = new CollectionPoint
        {
            Id = id, EarthquakeId = earthquakeId, Name = data.Name.Trim(), OrganizationName = Trim(data.OrganizationName),
            Address = data.Address.Trim(), Location = GeoPoint.FromCoordinates(data.Latitude, data.Longitude), Description = Trim(data.Description),
            NeedsSummary = data.NeedsSummary.Trim(), ReceivingInstructions = data.ReceivingInstructions.Trim(), ContactName = Trim(data.ContactName),
            ContactPhone = Trim(data.ContactPhone), ContactWhatsApp = Trim(data.ContactWhatsApp), ContactEmail = Trim(data.ContactEmail), EndsAt = data.EndsAt,
            Source = CollectionPointSource.Automated, ModerationStatus = CollectionPointModerationStatus.Pending
        };
        point.SearchText = Normalize(string.Join(' ', point.Name, point.OrganizationName, point.Address, point.NeedsSummary));
        db.CollectionPoints.Add(point);
        return (id, null);
    }

    private (Guid id, string? error) CreateBloodDonationCenter(IngestionBloodDonationCenterData data, Guid earthquakeId, Guid id)
    {
        var error = ValidateCommon(data.Name, data.Address, data.Description, data.NeedsSummary, data.OperatingInstructions);
        if (error is not null) return (id, error);
        if (!ValidCoordinates(data.Latitude, data.Longitude)) return (id, "Coordinates must be supplied together and be valid.");
        if (string.IsNullOrWhiteSpace(data.PublicPhone) || data.PublicPhone.Length > 80) return (id, "Public phone is required.");
        if (!Enum.IsDefined(data.CenterType) || data.BloodTypes == 0 || data.Components == 0) return (id, "Invalid blood donation data.");
        if (data.CenterType == BloodDonationCenterType.TemporaryCampaign && (data.StartsAt is null || data.EndsAt is null || data.EndsAt < data.StartsAt)) return (id, "Temporary campaigns require valid dates.");
        var center = new BloodDonationCenter
        {
            Id = id, EarthquakeId = earthquakeId, Name = data.Name.Trim(), OrganizationName = Trim(data.OrganizationName), Address = data.Address.Trim(),
            Location = GeoPoint.FromCoordinates(data.Latitude, data.Longitude), Description = Trim(data.Description), OperatingInstructions = data.OperatingInstructions.Trim(),
            NeedsSummary = data.NeedsSummary.Trim(), PublicPhone = data.PublicPhone.Trim(), PublicWhatsApp = Trim(data.PublicWhatsApp), PublicEmail = Trim(data.PublicEmail),
            CenterType = data.CenterType, BloodTypes = data.BloodTypes, Components = data.Components, StartsAt = data.StartsAt, EndsAt = data.EndsAt,
            Source = BloodDonationSource.Automated, ModerationStatus = BloodDonationModerationStatus.Pending
        };
        center.SearchText = Normalize(string.Join(' ', center.Name, center.OrganizationName, center.Address, center.NeedsSummary));
        db.BloodDonationCenters.Add(center);
        return (id, null);
    }

    private (Guid id, string? error) CreateShelter(IngestionShelterData data, Guid earthquakeId, Guid id)
    {
        var error = ValidateCommon(data.Name, data.Address, data.Description, data.Description, data.OperatingInstructions);
        if (error is not null) return (id, error);
        if (!ValidCoordinates(data.Latitude, data.Longitude)) return (id, "Coordinates must be supplied together and be valid.");
        var shelter = new Shelter
        {
            Id = id, EarthquakeId = earthquakeId, Name = data.Name.Trim(), OrganizationName = Trim(data.OrganizationName), Address = data.Address.Trim(),
            Location = GeoPoint.FromCoordinates(data.Latitude, data.Longitude), Description = data.Description.Trim(), OperatingInstructions = data.OperatingInstructions.Trim(),
            ContactName = Trim(data.ContactName), ContactPhone = Trim(data.ContactPhone), ContactWhatsApp = Trim(data.ContactWhatsApp), ContactEmail = Trim(data.ContactEmail),
            Source = ShelterSource.Automated, ModerationStatus = ShelterModerationStatus.Pending
        };
        shelter.SearchText = Normalize(string.Join(' ', shelter.Name, shelter.OrganizationName, shelter.Address, shelter.Description));
        db.Shelters.Add(shelter);
        return (id, null);
    }

    private (Guid id, string? error) CreateHelpRequest(IngestionHelpRequestData data, Guid earthquakeId, Guid id)
    {
        var error = ValidateCommon(data.Title, data.Address, data.NeedDetails, data.NeedDetails, data.Instructions);
        if (error is not null) return (id, error);
        if (string.IsNullOrWhiteSpace(data.RequesterName) || data.RequesterName.Length > 200) return (id, "Requester name is required.");
        if (!ValidCoordinates(data.Latitude, data.Longitude)) return (id, "Coordinates must be supplied together and be valid.");
        if (string.IsNullOrWhiteSpace(data.PublicPhone) || data.PublicPhone.Length > 80) return (id, "Public phone is required.");
        if (!Enum.IsDefined(data.Priority) || data.NeedCategories == 0) return (id, "Priority and at least one need category are required.");
        var request = new HelpRequest
        {
            Id = id, EarthquakeId = earthquakeId, Title = data.Title.Trim(), RequesterName = data.RequesterName.Trim(), OrganizationName = Trim(data.OrganizationName), Address = data.Address.Trim(),
            Location = GeoPoint.FromCoordinates(data.Latitude, data.Longitude), NeedDetails = data.NeedDetails.Trim(), Instructions = Trim(data.Instructions), PublicPhone = data.PublicPhone.Trim(),
            PublicWhatsApp = Trim(data.PublicWhatsApp), PublicEmail = Trim(data.PublicEmail), Priority = data.Priority, NeedCategories = data.NeedCategories, NeededBy = data.NeededBy,
            Source = HelpRequestSource.Automated, ModerationStatus = HelpRequestModerationStatus.Pending
        };
        request.SearchText = Normalize(string.Join(' ', request.Title, request.RequesterName, request.OrganizationName, request.Address, request.NeedDetails));
        db.HelpRequests.Add(request);
        return (id, null);
    }

    private static bool ValidateSource(IngestionSource source, out string? error)
    {
        error = null;
        if (!Enum.IsDefined(source.Platform) || !Uri.TryCreate(source.SourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) error = "A public HTTPS source URL is required.";
        else if (source.Confidence is < 0 or > 1) error = "Confidence must be between 0 and 1.";
        else if (source.SourceUrl.Length > 2000 || source.ExternalPostId?.Length > 300 || source.EvidenceSummary?.Length > 1000) error = "Source metadata is too long.";
        return error is null;
    }

    private static string? ValidateCommon(string name, string address, string? description, string requiredText, string? instructions)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) return "Name or title is required.";
        if (string.IsNullOrWhiteSpace(address) || address.Length > 400) return "Address is required.";
        if (string.IsNullOrWhiteSpace(requiredText) || requiredText.Length > MaxPageText) return "Required information is missing or too long.";
        if (description?.Length > 3000 || instructions?.Length > 2500) return "Submitted text is too long.";
        return null;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool ValidCoordinates(double? latitude, double? longitude) => latitude is null && longitude is null || latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : new(value.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))).Select(char.ToUpperInvariant).ToArray());
    private static string PublicPath(IngestionEntityType type, Guid id) => type switch
    {
        IngestionEntityType.CollectionPoint => $"/collection-points/{id}",
        IngestionEntityType.BloodDonationCenter => $"/donacion-sangre/{id}",
        IngestionEntityType.Shelter => $"/refugios/{id}",
        _ => $"/ayuda/{id}"
    };
}
