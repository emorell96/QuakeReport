using NetTopologySuite.Geometries;
using QuakeReport.Contracts.Enums;
using StorageGenerics.Core.Contracts;

namespace QuakeReport.Data.Models;

public class GeocodingReviewItem : IEntity<Guid>
{
    public Guid Id { get; set; }
    public required string EntityType { get; set; }
    public Guid EntityId { get; set; }
    public required string AddressSnapshot { get; set; }
    public required string AddressHash { get; set; }
    public GeocodingReviewStatus Status { get; set; } = GeocodingReviewStatus.NeedsReview;
    public required string Reason { get; set; }
    public Point? CandidateLocation { get; set; }
    public string? FormattedAddress { get; set; }
    public string? GooglePlaceId { get; set; }
    public string? Granularity { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset LastAttemptAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}
