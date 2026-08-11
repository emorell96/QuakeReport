using QuakeReport.Contracts.Enums;

namespace QuakeReport.Contracts.Dtos;

public record IngestionSource(
    IngestionPlatform Platform,
    string SourceUrl,
    string? ExternalPostId,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ExtractedAt,
    decimal Confidence,
    string? EvidenceSummary);

public record IngestionCollectionPointData(
    string Name, string? OrganizationName, string Address, double? Latitude, double? Longitude,
    string? Description, string NeedsSummary, string ReceivingInstructions, string? ContactName,
    string? ContactPhone, string? ContactWhatsApp, string? ContactEmail, DateTimeOffset? EndsAt);

public record IngestionBloodDonationCenterData(
    string Name, string? OrganizationName, string Address, double? Latitude, double? Longitude,
    string? Description, string OperatingInstructions, string NeedsSummary, string PublicPhone,
    string? PublicWhatsApp, string? PublicEmail, BloodDonationCenterType CenterType,
    BloodTypeFlags BloodTypes, BloodComponentFlags Components, DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt);

public record IngestionShelterData(
    string Name, string? OrganizationName, string Address, double? Latitude, double? Longitude,
    string Description, string OperatingInstructions, string? ContactName, string? ContactPhone,
    string? ContactWhatsApp, string? ContactEmail);

public record IngestionHelpRequestData(
    string Title, string RequesterName, string? OrganizationName, string Address, double? Latitude,
    double? Longitude, string NeedDetails, string? Instructions, string PublicPhone,
    string? PublicWhatsApp, string? PublicEmail, HelpRequestPriority Priority,
    HelpNeedCategory NeedCategories, DateTimeOffset? NeededBy);

public record IngestionCollectionPointRequest(IngestionSource Source, IngestionCollectionPointData Data);
public record IngestionBloodDonationCenterRequest(IngestionSource Source, IngestionBloodDonationCenterData Data);
public record IngestionShelterRequest(IngestionSource Source, IngestionShelterData Data);
public record IngestionHelpRequestRequest(IngestionSource Source, IngestionHelpRequestData Data);

public record IngestionSubmissionResponse(
    Guid SubmissionId, Guid EntityId, IngestionEntityType EntityType,
    string ModerationStatus, bool Duplicate, string PublicPath);
