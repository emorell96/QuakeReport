using QuakeReport.Contracts.Enums;

namespace QuakeReport.Data.Models;

/// <summary>A photo or video attached to a <see cref="DamageReport"/>.</summary>
public class ReportMedia
{
    public Guid Id { get; set; }

    public Guid DamageReportId { get; set; }

    public DamageReport? DamageReport { get; set; }

    public required string BlobUrl { get; set; }

    public required MediaType MediaType { get; set; }

    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public required long SizeBytes { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
