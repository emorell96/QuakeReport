using QuakeReport.Contracts.Enums;

namespace QuakeReport.Contracts.Dtos;

public record ReportMediaResponse(
    Guid Id,
    string BlobUrl,
    MediaType MediaType,
    DateTimeOffset UploadedAt);
