using QuakeReport.Contracts.Enums;

namespace QuakeReport.Contracts.Dtos;

public record HelpRequestSummaryResponse(
    Guid Id, string Title, string RequesterName, string? OrganizationName, string Address,
    string NeedDetails, HelpRequestPriority Priority, HelpNeedCategory NeedCategories,
    HelpRequestStatus Status, HelpRequestModerationStatus ModerationStatus, HelpRequestSource Source,
    DateTimeOffset? NeededBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    string GoogleMapsUrl, bool IsOverdue, bool IsStale);

public record HelpRequestResponse(
    Guid Id, Guid EarthquakeId, string Title, string RequesterName, string? OrganizationName,
    string Address, double? Latitude, double? Longitude, string NeedDetails, string? Instructions,
    string PublicPhone, string? PublicWhatsApp, string? PublicEmail, HelpRequestPriority Priority,
    HelpNeedCategory NeedCategories, HelpRequestStatus Status, HelpRequestModerationStatus ModerationStatus,
    HelpRequestSource Source, DateTimeOffset? NeededBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    string GoogleMapsUrl, bool IsOverdue, bool IsStale, IReadOnlyList<HelpRequestCommentResponse> Comments);

public record HelpRequestCommentResponse(Guid Id, string? DisplayName, string Message, DateTimeOffset CreatedAt);
public record CreateHelpRequestRequest(
    string Title, string RequesterName, string? OrganizationName, string Address, double? Latitude,
    double? Longitude, string NeedDetails, string? Instructions, string PublicPhone, string? PublicWhatsApp,
    string? PublicEmail, HelpRequestPriority Priority, HelpNeedCategory NeedCategories,
    DateTimeOffset? NeededBy, bool PublicContactConsent, string TurnstileToken);
public record UpdateHelpRequestRequest(
    string Title, string RequesterName, string? OrganizationName, string Address, double? Latitude,
    double? Longitude, string NeedDetails, string? Instructions, string PublicPhone, string? PublicWhatsApp,
    string? PublicEmail, HelpRequestPriority Priority, HelpNeedCategory NeedCategories, DateTimeOffset? NeededBy);
public record CreateHelpRequestCommentRequest(string? DisplayName, string Message, string TurnstileToken);
public record UpdateHelpRequestStatusRequest(HelpRequestStatus Status);
public record UpdateHelpRequestModerationRequest(HelpRequestModerationStatus Status);
public record HelpRequestManagementCodeRequest(string ManagementCode);
public record HelpRequestAbuseReportRequest(string Reason, string? Details, string TurnstileToken);
public record CreateHelpRequestResponse(HelpRequestResponse Request, string ManagementCode);
