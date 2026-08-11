using QuakeReport.Contracts.Dtos;
using QuakeReport.Data.Models;

namespace QuakeReport.ApiService.Dtos;

public static class ResponseMappingExtensions
{
    public static ReportMediaResponse ToResponse(this ReportMedia media) => new(
        media.Id,
        media.BlobUrl,
        media.MediaType,
        media.UploadedAt);

    public static DamageReportResponse ToResponse(this DamageReport report) => new(
        report.Id,
        report.EarthquakeId,
        report.Description,
        report.Severity,
        report.DamageSigns,
        report.StructureType,
        report.StructureSize,
        report.Latitude,
        report.Longitude,
        report.Address,
        report.CreatedAt,
        report.Media.Select(m => m.ToResponse()).ToList());

    public static DamageReportSummaryResponse ToSummaryResponse(this DamageReport report) => new(
        report.Id,
        report.EarthquakeId,
        report.Description,
        report.Severity,
        report.DamageSigns,
        report.StructureType,
        report.StructureSize,
        report.Latitude,
        report.Longitude,
        report.Address,
        report.CreatedAt);

    public static EarthquakeResponse ToResponse(this Earthquake earthquake) => new(
        earthquake.Id,
        earthquake.Name,
        earthquake.Magnitude,
        earthquake.OccurredAt,
        earthquake.EpicenterLatitude,
        earthquake.EpicenterLongitude);

    public static MissingPersonSummaryResponse ToSummaryResponse(this MissingPerson person) => new(
        person.Id, person.FullName, person.ApproximateAge, person.IdentificationDocumentType,
        person.IdentificationLastFour, person.Status, person.LastSeenAt, person.Description,
        person.PhotoUrl, person.Locations.OrderBy(location => location.Id).Select(location => location.Address).FirstOrDefault(), person.CreatedAt);

    public static MissingPersonResponse ToResponse(this MissingPerson person) => new(
        person.Id, person.EarthquakeId, person.FullName, person.Aliases, person.ApproximateAge,
        person.IdentificationDocumentType, person.IdentificationLastFour, person.Description,
        person.PhysicalDescription, person.ClothingDescription, person.Status, person.LastSeenAt,
        person.PhotoUrl, person.Locations.Select(location => new MissingPersonLocationDto(
            location.Id, location.Address, location.Latitude, location.Longitude, location.Note)).ToList(),
        person.CreatedAt, person.UpdatedAt);

    public static MissingPersonTipResponse ToPublicResponse(this MissingPersonTip tip) => new(
        tip.Id, tip.Message, tip.SightedAt, tip.Address, tip.Latitude, tip.Longitude, tip.CreatedAt);

    public static PrivateMissingPersonTipResponse ToPrivateResponse(this MissingPersonTip tip) => new(
        tip.Id, tip.Message, tip.SightedAt, tip.Address, tip.Latitude, tip.Longitude,
        tip.ResponderName, tip.ResponderPhone, tip.ResponderEmail, tip.CreatedAt, tip.IsHidden);

    public static CollectionPointSummaryResponse ToSummaryResponse(this CollectionPoint point) => new(
        point.Id, point.Name, point.OrganizationName, point.Address, point.NeedsSummary,
        point.ModerationStatus, point.OperationalStatus, point.Source, point.EndsAt, point.UpdatedAt,
        point.GoogleMapsUrl(), point.EndsAt is not null && point.EndsAt < DateTimeOffset.UtcNow);

    public static CollectionPointResponse ToResponse(this CollectionPoint point, IReadOnlyList<CollectionPointCommentResponse>? comments = null) => new(
        point.Id, point.EarthquakeId, point.Name, point.OrganizationName, point.Address, point.Latitude, point.Longitude,
        point.Description, point.NeedsSummary, point.ReceivingInstructions, point.ContactName, point.ContactPhone,
        point.ContactWhatsApp, point.ContactEmail, point.EndsAt, point.ModerationStatus, point.OperationalStatus,
        point.Source, point.CreatedAt, point.UpdatedAt, point.GoogleMapsUrl(),
        point.EndsAt is not null && point.EndsAt < DateTimeOffset.UtcNow, comments ?? []);

    public static CollectionPointCommentResponse ToResponse(this CollectionPointComment comment) =>
        new(comment.Id, comment.DisplayName, comment.Message, comment.CreatedAt);

    public static PrivateCollectionPointCommentResponse ToPrivateResponse(this CollectionPointComment comment) =>
        new(comment.Id, comment.DisplayName, comment.Message, comment.CreatedAt, comment.IsHidden);

    public static string GoogleMapsUrl(this CollectionPoint point) =>
        point.Latitude.HasValue && point.Longitude.HasValue
            ? $"https://www.google.com/maps/search/?api=1&query={point.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{point.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(point.Address)}";

    public static ShelterSummaryResponse ToSummaryResponse(this Shelter shelter) => new(
        shelter.Id, shelter.Name, shelter.OrganizationName, shelter.Address, shelter.Description,
        shelter.OperatingInstructions, shelter.ModerationStatus, shelter.OperationalStatus,
        shelter.Source, shelter.UpdatedAt, shelter.GoogleMapsUrl());

    public static ShelterResponse ToResponse(this Shelter shelter) => new(
        shelter.Id, shelter.EarthquakeId, shelter.Name, shelter.OrganizationName, shelter.Address,
        shelter.Latitude, shelter.Longitude, shelter.Description, shelter.OperatingInstructions,
        shelter.ContactName, shelter.ContactPhone, shelter.ContactWhatsApp, shelter.ContactEmail,
        shelter.ModerationStatus, shelter.OperationalStatus, shelter.Source, shelter.CreatedAt,
        shelter.UpdatedAt, shelter.GoogleMapsUrl());

    public static string GoogleMapsUrl(this Shelter shelter) =>
        shelter.Latitude.HasValue && shelter.Longitude.HasValue
            ? $"https://www.google.com/maps/search/?api=1&query={shelter.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{shelter.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(shelter.Address)}";

    public static HelpRequestSummaryResponse ToSummaryResponse(this HelpRequest request) => new(
        request.Id, request.Title, request.RequesterName, request.OrganizationName, request.Address,
        request.NeedDetails, request.Priority, request.NeedCategories, request.Status,
        request.ModerationStatus, request.Source, request.NeededBy, request.CreatedAt, request.UpdatedAt,
        request.GoogleMapsUrl(), request.NeededBy is not null && request.NeededBy < DateTimeOffset.UtcNow,
        request.UpdatedAt < DateTimeOffset.UtcNow.AddHours(-12));

    public static HelpRequestResponse ToResponse(this HelpRequest request, IReadOnlyList<HelpRequestCommentResponse>? comments = null) => new(
        request.Id, request.EarthquakeId, request.Title, request.RequesterName, request.OrganizationName,
        request.Address, request.Latitude, request.Longitude, request.NeedDetails, request.Instructions,
        request.PublicPhone, request.PublicWhatsApp, request.PublicEmail, request.Priority,
        request.NeedCategories, request.Status, request.ModerationStatus, request.Source, request.NeededBy,
        request.CreatedAt, request.UpdatedAt, request.GoogleMapsUrl(),
        request.NeededBy is not null && request.NeededBy < DateTimeOffset.UtcNow,
        request.UpdatedAt < DateTimeOffset.UtcNow.AddHours(-12), comments ?? []);

    public static HelpRequestCommentResponse ToResponse(this HelpRequestComment comment) =>
        new(comment.Id, comment.DisplayName, comment.Message, comment.CreatedAt);

    public static string GoogleMapsUrl(this HelpRequest request) =>
        request.Latitude.HasValue && request.Longitude.HasValue
            ? $"https://www.google.com/maps/search/?api=1&query={request.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{request.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(request.Address)}";

    public static BloodDonationCenterSummaryResponse ToSummaryResponse(this BloodDonationCenter center) => new(center.Id, center.Name, center.OrganizationName, center.Address, center.CenterType, center.BloodTypes, center.Components, center.OperationalStatus, center.ModerationStatus, center.Source, center.StartsAt, center.EndsAt, center.UpdatedAt, center.GoogleMapsUrl(), center.EndsAt is not null && center.EndsAt < DateTimeOffset.UtcNow);
    public static BloodDonationCenterResponse ToResponse(this BloodDonationCenter center, IReadOnlyList<BloodDonationCenterCommentResponse>? comments = null) => new(center.Id, center.EarthquakeId, center.Name, center.OrganizationName, center.Address, center.Latitude, center.Longitude, center.Description, center.OperatingInstructions, center.NeedsSummary, center.PublicPhone, center.PublicWhatsApp, center.PublicEmail, center.CenterType, center.BloodTypes, center.Components, center.OperationalStatus, center.ModerationStatus, center.Source, center.StartsAt, center.EndsAt, center.CreatedAt, center.UpdatedAt, center.GoogleMapsUrl(), center.EndsAt is not null && center.EndsAt < DateTimeOffset.UtcNow, comments ?? []);
    public static BloodDonationCenterCommentResponse ToResponse(this BloodDonationCenterComment comment) => new(comment.Id, comment.DisplayName, comment.Message, comment.CreatedAt);
    public static string GoogleMapsUrl(this BloodDonationCenter center) => center.Latitude.HasValue && center.Longitude.HasValue ? $"https://www.google.com/maps/search/?api=1&query={center.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{center.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(center.Address)}";
}
