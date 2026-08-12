using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;

namespace QuakeReport.ApiService.Controllers;

[ApiController]
[Route("api/shelters")]
public class SheltersController(
    QuakeReportDbContext db,
    ActiveEarthquakeService earthquakes,
    ITurnstileValidator turnstile,
    IConfiguration configuration) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? query = null,
        [FromQuery] ShelterOperationalStatus? operationalStatus = null,
        [FromQuery] ShelterModerationStatus? moderationStatus = null,
        [FromQuery] ShelterSortOption sort = ShelterSortOption.Newest,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize || !Enum.IsDefined(sort) ||
            (operationalStatus is not null && !Enum.IsDefined(operationalStatus.Value)) ||
            (moderationStatus is not null && !Enum.IsDefined(moderationStatus.Value)))
            return BadRequest("Invalid shelter query parameters.");

        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null)
            return Ok(new PagedResponse<ShelterSummaryResponse>([], page, pageSize, 0, 0));

        var shelters = db.Shelters.AsNoTracking().Where(shelter =>
            shelter.EarthquakeId == earthquake.Id && shelter.ModerationStatus != ShelterModerationStatus.Rejected);
        if (operationalStatus is not null) shelters = shelters.Where(shelter => shelter.OperationalStatus == operationalStatus);
        else shelters = shelters.Where(shelter => shelter.OperationalStatus != ShelterOperationalStatus.Closed);
        if (moderationStatus is not null) shelters = shelters.Where(shelter => shelter.ModerationStatus == moderationStatus);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = NormalizeSearch(query);
            shelters = shelters.Where(shelter => shelter.SearchText!.Contains(normalized));
        }

        var total = await shelters.CountAsync(cancellationToken);
        var ordered = sort switch
        {
            ShelterSortOption.RecentlyUpdated => shelters.OrderByDescending(shelter => shelter.UpdatedAt).ThenByDescending(shelter => shelter.Id),
            ShelterSortOption.Name => shelters.OrderBy(shelter => shelter.Name).ThenBy(shelter => shelter.Id),
            _ => shelters.OrderByDescending(shelter => shelter.CreatedAt).ThenByDescending(shelter => shelter.Id),
        };
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new PagedResponse<ShelterSummaryResponse>(items.Select(shelter => shelter.ToSummaryResponse()).ToList(), page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var shelter = await db.Shelters.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == id && item.ModerationStatus != ShelterModerationStatus.Rejected, cancellationToken);
        return shelter is null ? NotFound() : Ok(shelter.ToResponse());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateShelterRequest request, CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return BadRequest(validation);
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable) return StatusCode(503, "Verification service unavailable.");
        if (!challenge.Success) return BadRequest("Human verification failed.");
        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null) return UnprocessableEntity("No active earthquake is configured.");

        var code = MissingPersonSecurity.CreateManagementCode();
        var shelter = CreateEntity(request, earthquake.Id, ShelterSource.Community, ShelterModerationStatus.Pending, code);
        db.Shelters.Add(shelter);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = shelter.Id }, new CreateShelterResponse(shelter.ToResponse(), code));
    }

    [HttpPost("management/lookup")]
    public async Task<IActionResult> LookupManagementCode(ShelterManagementCodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ManagementCode)) return BadRequest("Management code is required.");
        var hash = MissingPersonSecurity.HashManagementCode(request.ManagementCode);
        var shelter = await db.Shelters.AsNoTracking().SingleOrDefaultAsync(item =>
            item.ManagementCodeHash == hash && item.ModerationStatus != ShelterModerationStatus.Rejected, cancellationToken);
        return shelter is null ? NotFound() : Ok(shelter.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateShelterRequest request,
        [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var shelter = await db.Shelters.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (shelter is null) return NotFound();
        if (!Authorize(code, shelter)) return Unauthorized();
        var validation = Validate(request);
        if (validation is not null) return BadRequest(validation);
        Apply(shelter, request);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(shelter.ToResponse());
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, UpdateShelterStatusRequest request,
        [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status)) return BadRequest("Invalid status.");
        var shelter = await db.Shelters.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (shelter is null) return NotFound();
        if (!Authorize(code, shelter)) return Unauthorized();
        shelter.OperationalStatus = request.Status;
        shelter.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(shelter.ToResponse());
    }

    [HttpPost("{id:guid}/abuse-reports")]
    public async Task<IActionResult> Abuse(Guid id, ShelterAbuseReportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 200) return BadRequest("A reason is required.");
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable) return StatusCode(503, "Verification service unavailable.");
        if (!challenge.Success) return BadRequest("Human verification failed.");
        if (!await db.Shelters.AnyAsync(shelter => shelter.Id == id && shelter.ModerationStatus != ShelterModerationStatus.Rejected, cancellationToken)) return NotFound();
        db.ShelterAbuseReports.Add(new ShelterAbuseReport { Id = Guid.NewGuid(), ShelterId = id, Reason = request.Reason.Trim(), Details = request.Details?.Trim() });
        await db.SaveChangesAsync(cancellationToken);
        return Accepted();
    }

    [HttpGet("moderation/pending")]
    public async Task<IActionResult> Pending([FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        if (!Moderated(key)) return Unauthorized();
        if (page < 1 || pageSize is < 1 or > MaxPageSize) return BadRequest("Invalid pagination.");
        var shelters = db.Shelters.AsNoTracking().Where(shelter => shelter.ModerationStatus == ShelterModerationStatus.Pending);
        var total = await shelters.CountAsync(cancellationToken);
        var items = await shelters.OrderBy(shelter => shelter.CreatedAt).ThenBy(shelter => shelter.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new PagedResponse<ShelterSummaryResponse>(items.Select(shelter => shelter.ToSummaryResponse()).ToList(), page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }

    [HttpPost("moderation/official")]
    public async Task<IActionResult> CreateOfficial(CreateShelterRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        [FromHeader(Name = "X-Moderator-Email")] string? moderator,
        CancellationToken cancellationToken)
    {
        if (!Moderated(key)) return Unauthorized();
        var validation = Validate(request);
        if (validation is not null) return BadRequest(validation);
        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null) return UnprocessableEntity("No active earthquake is configured.");
        var shelter = CreateEntity(request, earthquake.Id, ShelterSource.Official, ShelterModerationStatus.Approved, null);
        shelter.ModeratedAt = DateTimeOffset.UtcNow;
        shelter.ModeratedBy = moderator?.Trim();
        db.Shelters.Add(shelter);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = shelter.Id }, shelter.ToResponse());
    }

    [HttpPut("moderation/{id:guid}")]
    public async Task<IActionResult> ModeratorUpdate(Guid id, UpdateShelterRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key, CancellationToken cancellationToken)
    {
        if (!Moderated(key)) return Unauthorized();
        var validation = Validate(request);
        if (validation is not null) return BadRequest(validation);
        var shelter = await db.Shelters.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (shelter is null) return NotFound();
        Apply(shelter, request);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(shelter.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}")]
    public async Task<IActionResult> Moderate(Guid id, UpdateShelterModerationRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        [FromHeader(Name = "X-Moderator-Email")] string? moderator,
        CancellationToken cancellationToken)
    {
        if (!Moderated(key)) return Unauthorized();
        if (!Enum.IsDefined(request.Status)) return BadRequest("Invalid moderation status.");
        var shelter = await db.Shelters.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (shelter is null) return NotFound();
        shelter.ModerationStatus = request.Status;
        shelter.ModeratedAt = DateTimeOffset.UtcNow;
        shelter.ModeratedBy = moderator?.Trim();
        shelter.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(shelter.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}/status")]
    public async Task<IActionResult> ModeratorStatus(Guid id, UpdateShelterStatusRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key, CancellationToken cancellationToken)
    {
        if (!Moderated(key)) return Unauthorized();
        if (!Enum.IsDefined(request.Status)) return BadRequest("Invalid status.");
        var shelter = await db.Shelters.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (shelter is null) return NotFound();
        shelter.OperationalStatus = request.Status;
        shelter.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(shelter.ToResponse());
    }

    private bool Moderated(string? supplied) => !string.IsNullOrWhiteSpace(supplied) &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(configuration["Moderation:ApiKey"] ?? "__missing__"));

    private static bool Authorize(string? code, Shelter shelter) =>
        !string.IsNullOrWhiteSpace(code) && shelter.ManagementCodeHash is not null && MissingPersonSecurity.Matches(code, shelter.ManagementCodeHash);

    private static Shelter CreateEntity(CreateShelterRequest request, Guid earthquakeId, ShelterSource source, ShelterModerationStatus moderation, string? code)
    {
        var shelter = new Shelter
        {
            Id = Guid.NewGuid(), EarthquakeId = earthquakeId, Source = source, ModerationStatus = moderation,
            ManagementCodeHash = code is null ? null : MissingPersonSecurity.HashManagementCode(code),
            Name = request.Name.Trim(), OrganizationName = request.OrganizationName?.Trim(), Address = request.Address.Trim(),
            Latitude = request.Latitude, Longitude = request.Longitude, Description = request.Description.Trim(),
            OperatingInstructions = request.OperatingInstructions.Trim(), ContactName = request.ContactName?.Trim(),
            ContactPhone = request.ContactPhone?.Trim(), ContactWhatsApp = request.ContactWhatsApp?.Trim(), ContactEmail = request.ContactEmail?.Trim(),
        };
        shelter.SearchText = NormalizeSearch(string.Join(' ', shelter.Name, shelter.OrganizationName, shelter.Address, shelter.Description));
        return shelter;
    }

    private static void Apply(Shelter shelter, UpdateShelterRequest request)
    {
        shelter.Name = request.Name.Trim(); shelter.OrganizationName = request.OrganizationName?.Trim();
        shelter.Address = request.Address.Trim(); shelter.Latitude = request.Latitude; shelter.Longitude = request.Longitude;
        shelter.Description = request.Description.Trim(); shelter.OperatingInstructions = request.OperatingInstructions.Trim();
        shelter.ContactName = request.ContactName?.Trim(); shelter.ContactPhone = request.ContactPhone?.Trim();
        shelter.ContactWhatsApp = request.ContactWhatsApp?.Trim(); shelter.ContactEmail = request.ContactEmail?.Trim();
        shelter.SearchText = NormalizeSearch(string.Join(' ', shelter.Name, shelter.OrganizationName, shelter.Address, shelter.Description));
        shelter.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? Validate(CreateShelterRequest request) =>
        !request.PrivacyConsent ? "Privacy consent is required." : ValidateCore(request.Name, request.Address, request.Description, request.OperatingInstructions);
    private static string? Validate(UpdateShelterRequest request) =>
        ValidateCore(request.Name, request.Address, request.Description, request.OperatingInstructions);
    private static string? ValidateCore(string name, string address, string description, string instructions)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) return "Name is required.";
        if (string.IsNullOrWhiteSpace(address) || address.Length > 400) return "Address is required.";
        if (string.IsNullOrWhiteSpace(description) || description.Length > 3000) return "Description is required.";
        if (string.IsNullOrWhiteSpace(instructions) || instructions.Length > 2000) return "Operating instructions are required.";
        return null;
    }

    private static string NormalizeSearch(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty :
        new(value.Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character)))
            .Select(char.ToUpperInvariant).ToArray());
}
