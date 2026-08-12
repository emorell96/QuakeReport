using QuakeReport.Contracts.Enums;

namespace QuakeReport.Contracts.Dtos;

public record GeocodingReviewItemResponse(
    Guid Id, string EntityType, Guid EntityId, string Address, GeocodingReviewStatus Status,
    string Reason, double? CandidateLatitude, double? CandidateLongitude, string? FormattedAddress,
    string? GooglePlaceId, string? Granularity, int AttemptCount, DateTimeOffset LastAttemptAt);

public record ResolveGeocodingReviewRequest(double Latitude, double Longitude, string? ResolvedBy);
public record DismissGeocodingReviewRequest(string? ResolvedBy);
