using QuakeReport.Contracts.Enums;
using NetTopologySuite.Geometries;

namespace QuakeReport.Data.Models;

public class HelpRequest : IGeocodableEntity
{
    public Guid Id { get; set; }
    public required Guid EarthquakeId { get; set; }
    public Earthquake? Earthquake { get; set; }
    public required string Title { get; set; }
    public required string RequesterName { get; set; }
    public string? OrganizationName { get; set; }
    public required string Address { get; set; }
    public string? SearchText { get; set; }
    public Point? Location { get; set; }
    public required string NeedDetails { get; set; }
    public string? Instructions { get; set; }
    public required string PublicPhone { get; set; }
    public string? PublicWhatsApp { get; set; }
    public string? PublicEmail { get; set; }
    public HelpRequestPriority Priority { get; set; }
    public HelpNeedCategory NeedCategories { get; set; }
    public HelpRequestStatus Status { get; set; } = HelpRequestStatus.Active;
    public HelpRequestModerationStatus ModerationStatus { get; set; } = HelpRequestModerationStatus.Pending;
    public HelpRequestSource Source { get; set; } = HelpRequestSource.Community;
    public DateTimeOffset? NeededBy { get; set; }
    public string? ManagementCodeHash { get; set; }
    public DateTimeOffset? ModeratedAt { get; set; }
    public string? ModeratedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<HelpRequestComment> Comments { get; set; } = [];
    public List<HelpRequestAbuseReport> AbuseReports { get; set; } = [];
}
