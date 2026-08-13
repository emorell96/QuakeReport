using QuakeReport.Contracts.Enums;

namespace QuakeReport.Core.Models.API;

public sealed record PaginationRequest
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

public sealed record GeoPointQuery
{
    public double Latitude { get; init; }

    public double Longitude { get; init; }
}

public sealed record BloodDonationCenterSearchFilter
{
    public Guid? EarthquakeId { get; init; }
    public string? SearchText { get; init; }
    public BloodDonationCenterType? CenterType { get; init; }
    public BloodDonationOperationalStatus? OperationalStatus { get; init; }
    public BloodDonationModerationStatus? ModerationStatus { get; init; }
    public BloodTypeFlags? BloodTypes { get; init; }
    public BloodComponentFlags? Components { get; init; }
    public BloodDonationSortOption Sort { get; init; } = BloodDonationSortOption.Newest;
    public GeoPointQuery? CenterPoint { get; init; }
}

public sealed record CollectionPointSearchFilter
{
    public Guid? EarthquakeId { get; init; }
    public string? SearchText { get; init; }
    public CollectionPointOperationalStatus? OperationalStatus { get; init; }
    public CollectionPointModerationStatus? ModerationStatus { get; init; }
    public CollectionPointSortOption Sort { get; init; } = CollectionPointSortOption.Newest;
    public GeoPointQuery? CenterPoint { get; init; }
}

public sealed record ShelterSearchFilter
{
    public Guid? EarthquakeId { get; init; }
    public string? SearchText { get; init; }
    public ShelterOperationalStatus? OperationalStatus { get; init; }
    public ShelterModerationStatus? ModerationStatus { get; init; }
    public ShelterSortOption Sort { get; init; } = ShelterSortOption.Newest;
    public GeoPointQuery? CenterPoint { get; init; }
}

public sealed record HelpRequestSearchFilter
{
    public Guid? EarthquakeId { get; init; }
    public string? SearchText { get; init; }
    public HelpRequestPriority? Priority { get; init; }
    public HelpNeedCategory? Category { get; init; }
    public HelpRequestStatus? Status { get; init; }
    public HelpRequestModerationStatus? ModerationStatus { get; init; }
    public HelpRequestSortOption Sort { get; init; } = HelpRequestSortOption.HighestPriority;
    public GeoPointQuery? CenterPoint { get; init; }
}

public sealed record MissingPersonSearchFilter
{
    public Guid? EarthquakeId { get; init; }
    public string? SearchText { get; init; }
    public MissingPersonStatus? Status { get; init; }
    public MissingPersonSortOption Sort { get; init; } = MissingPersonSortOption.Newest;
}

public sealed record DamageReportSearchFilter
{
    public Guid? EarthquakeId { get; init; }
    public SeverityLevel? Severity { get; init; }
    public ReportSortOption Sort { get; init; } = ReportSortOption.Newest;
}
