using QuakeReport.Contracts.Enums;

namespace QuakeReport.Contracts.Dtos;

public record CollectionPointSummaryResponse(
    Guid Id, string Name, string? OrganizationName, string Address, string? NeedsSummary,
    CollectionPointModerationStatus ModerationStatus, CollectionPointOperationalStatus OperationalStatus,
    CollectionPointSource Source, DateTimeOffset? EndsAt, DateTimeOffset UpdatedAt,
    string GoogleMapsUrl, bool IsExpired);

public record CollectionPointResponse(
    Guid Id, Guid EarthquakeId, string Name, string? OrganizationName, string Address,
    double? Latitude, double? Longitude, string? Description, string NeedsSummary,
    string ReceivingInstructions, string? ContactName, string? ContactPhone, string? ContactWhatsApp,
    string? ContactEmail, DateTimeOffset? EndsAt, CollectionPointModerationStatus ModerationStatus,
    CollectionPointOperationalStatus OperationalStatus, CollectionPointSource Source,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string GoogleMapsUrl, bool IsExpired,
    IReadOnlyList<CollectionPointCommentResponse> Comments);

public record CollectionPointCommentResponse(Guid Id, string? DisplayName, string Message, DateTimeOffset CreatedAt);
public record PrivateCollectionPointCommentResponse(Guid Id, string? DisplayName, string Message, DateTimeOffset CreatedAt, bool IsHidden);

public record CreateCollectionPointRequest(
    string Name, string? OrganizationName, string Address, double? Latitude, double? Longitude,
    string? Description, string NeedsSummary, string ReceivingInstructions, string? ContactName,
    string? ContactPhone, string? ContactWhatsApp, string? ContactEmail, DateTimeOffset? EndsAt,
    string TurnstileToken);

public record UpdateCollectionPointRequest(
    string Name, string? OrganizationName, string Address, double? Latitude, double? Longitude,
    string? Description, string NeedsSummary, string ReceivingInstructions, string? ContactName,
    string? ContactPhone, string? ContactWhatsApp, string? ContactEmail, DateTimeOffset? EndsAt);

public record CreateCollectionPointCommentRequest(string? DisplayName, string Message, string TurnstileToken);
public record UpdateCollectionPointStatusRequest(CollectionPointOperationalStatus Status);
public record UpdateCollectionPointModerationRequest(CollectionPointModerationStatus Status);
public record CollectionPointManagementCodeRequest(string ManagementCode);
public record CollectionPointAbuseReportRequest(string Reason, string? Details, string TurnstileToken);
public record CreateCollectionPointResponse(CollectionPointResponse Point, string ManagementCode);
