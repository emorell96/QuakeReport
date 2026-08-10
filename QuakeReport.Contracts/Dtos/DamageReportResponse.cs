using QuakeReport.Contracts.Enums;

namespace QuakeReport.Contracts.Dtos;

public record DamageReportResponse(
    Guid Id,
    Guid EarthquakeId,
    string Description,
    SeverityLevel Severity,
    DamageSign DamageSigns,
    StructureType? StructureType,
    StructureSize? StructureSize,
    double Latitude,
    double Longitude,
    string? Address,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ReportMediaResponse> Media);
