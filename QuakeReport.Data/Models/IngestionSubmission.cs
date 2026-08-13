using QuakeReport.Contracts.Enums;
using StorageGenerics.Core.Contracts;

namespace QuakeReport.Data.Models;

public class IngestionSubmission : IEntity<Guid>
{
    public Guid Id { get; set; }
    public required Guid EarthquakeId { get; set; }
    public Earthquake? Earthquake { get; set; }
    public IngestionEntityType EntityType { get; set; }
    public required Guid EntityId { get; set; }
    public IngestionPlatform Platform { get; set; }
    public required string SourceUrl { get; set; }
    public string? ExternalPostId { get; set; }
    public required string IdempotencyKeyHash { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset ExtractedAt { get; set; }
    public decimal Confidence { get; set; }
    public string? EvidenceSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
