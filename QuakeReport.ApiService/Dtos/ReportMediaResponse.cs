using QuakeReport.Data.Models;

namespace QuakeReport.ApiService.Dtos;

public record ReportMediaResponse(
    Guid Id,
    string BlobUrl,
    MediaType MediaType,
    DateTimeOffset UploadedAt)
{
    public static ReportMediaResponse FromEntity(ReportMedia media) => new(
        media.Id,
        media.BlobUrl,
        media.MediaType,
        media.UploadedAt);
}
