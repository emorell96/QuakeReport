using QuakeReport.Data.Models;

namespace QuakeReport.ApiService.Dtos;

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
    IReadOnlyList<ReportMediaResponse> Media)
{
    public static DamageReportResponse FromEntity(DamageReport report) => new(
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
        report.Media.Select(ReportMediaResponse.FromEntity).ToList());
}
