using QuakeReport.Contracts.Enums;

namespace QuakeReport.Contracts.Dtos;

public record ShelterSummaryResponse(
    Guid Id, string Name, string? OrganizationName, string Address, string Description,
    string OperatingInstructions, ShelterModerationStatus ModerationStatus,
    ShelterOperationalStatus OperationalStatus, ShelterSource Source,
    DateTimeOffset UpdatedAt, string GoogleMapsUrl);

public record ShelterResponse(
    Guid Id, Guid EarthquakeId, string Name, string? OrganizationName, string Address,
    double? Latitude, double? Longitude, string Description, string OperatingInstructions,
    string? ContactName, string? ContactPhone, string? ContactWhatsApp, string? ContactEmail,
    ShelterModerationStatus ModerationStatus, ShelterOperationalStatus OperationalStatus,
    ShelterSource Source, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string GoogleMapsUrl);

public record CreateShelterRequest(
    string Name, string? OrganizationName, string Address, double? Latitude, double? Longitude,
    string Description, string OperatingInstructions, string? ContactName, string? ContactPhone,
    string? ContactWhatsApp, string? ContactEmail, string TurnstileToken);

public record UpdateShelterRequest(
    string Name, string? OrganizationName, string Address, double? Latitude, double? Longitude,
    string Description, string OperatingInstructions, string? ContactName, string? ContactPhone,
    string? ContactWhatsApp, string? ContactEmail);

public record CreateShelterResponse(ShelterResponse Shelter, string ManagementCode);
public record ShelterManagementCodeRequest(string ManagementCode);
public record UpdateShelterStatusRequest(ShelterOperationalStatus Status);
public record UpdateShelterModerationRequest(ShelterModerationStatus Status);
public record ShelterAbuseReportRequest(string Reason, string? Details, string TurnstileToken);
