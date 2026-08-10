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

    public static EarthquakeResponse ToResponse(this Earthquake earthquake) => new(
        earthquake.Id,
        earthquake.Name,
        earthquake.Magnitude,
        earthquake.OccurredAt,
        earthquake.EpicenterLatitude,
        earthquake.EpicenterLongitude);
}
