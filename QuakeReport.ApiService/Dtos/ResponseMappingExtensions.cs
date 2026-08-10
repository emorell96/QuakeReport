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
}
