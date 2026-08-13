using QuakeReport.Contracts.Enums;

namespace QuakeReport.Contracts.Dtos;

public record MissingPersonLocationDto(
    Guid Id,
    string Address,
    double? Latitude,
    double? Longitude,
    string? Note);

public record MissingPersonSummaryResponse(
    Guid Id,
    string FullName,
    string? ApproximateAge,
    IdentificationDocumentType? IdentificationDocumentType,
    string? IdentificationLastFour,
    MissingPersonStatus Status,
    DateTimeOffset LastSeenAt,
    string Description,
    string? PhotoUrl,
    string? PrimaryAddress,
    DateTimeOffset CreatedAt);

public record MissingPersonResponse(
    Guid Id,
    Guid EarthquakeId,
    string FullName,
    string? Aliases,
    string? ApproximateAge,
    IdentificationDocumentType? IdentificationDocumentType,
    string? IdentificationLastFour,
    string Description,
    string? PhysicalDescription,
    string? ClothingDescription,
    MissingPersonStatus Status,
    DateTimeOffset LastSeenAt,
    string? PhotoUrl,
    IReadOnlyList<MissingPersonLocationDto> Locations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record MissingPersonTipResponse(
    Guid Id,
    string Message,
    DateTimeOffset? SightedAt,
    string? Address,
    double? Latitude,
    double? Longitude,
    DateTimeOffset CreatedAt);

public record PrivateMissingPersonTipResponse(
    Guid Id,
    string Message,
    DateTimeOffset? SightedAt,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? ResponderName,
    string? ResponderPhone,
    string? ResponderEmail,
    DateTimeOffset CreatedAt,
    bool IsHidden);

public record CreateMissingPersonRequest(
    string FullName,
    string? Aliases,
    string? ApproximateAge,
    IdentificationDocumentType? IdentificationDocumentType,
    string? IdentificationNumber,
    string Description,
    string? PhysicalDescription,
    string? ClothingDescription,
    DateTimeOffset LastSeenAt,
    IReadOnlyList<CreateMissingPersonLocationRequest> Locations,
    bool PublicationConsent,
    string TurnstileToken);

public record CreateMissingPersonLocationRequest(
    string Address,
    double? Latitude,
    double? Longitude,
    string? Note);

public record CreateMissingPersonResponse(
    MissingPersonResponse Person,
    string ManagementCode);

public record MissingPersonPhotoResponse(string PhotoUrl);

public record CreateMissingPersonTipRequest(
    string Message,
    DateTimeOffset? SightedAt,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? ResponderName,
    string? ResponderPhone,
    string? ResponderEmail,
    string TurnstileToken);

public record UpdateMissingPersonStatusRequest(MissingPersonStatus Status);

public record UpdateMissingPersonRequest(
    string FullName,
    string? Aliases,
    string? ApproximateAge,
    string Description,
    string? PhysicalDescription,
    string? ClothingDescription,
    DateTimeOffset LastSeenAt,
    IReadOnlyList<CreateMissingPersonLocationRequest> Locations);

public record ManagementCodeRequest(string ManagementCode);

public record IdentificationLookupRequest(
    IdentificationDocumentType DocumentType,
    string IdentificationNumber,
    string TurnstileToken);

public record AbuseReportRequest(string Reason, string? Details, string TurnstileToken);
