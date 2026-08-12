using QuakeReport.Contracts.Enums;
using NetTopologySuite.Geometries;

namespace QuakeReport.Data.Models;

public class CollectionPoint : IGeocodableEntity
{
    public Guid Id { get; set; }
    public required Guid EarthquakeId { get; set; }
    public Earthquake? Earthquake { get; set; }
    public required string Name { get; set; }
    public string? OrganizationName { get; set; }
    public required string Address { get; set; }
    public string? SearchText { get; set; }
    public Point? Location { get; set; }
    public string? Description { get; set; }
    public required string NeedsSummary { get; set; }
    public required string ReceivingInstructions { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactWhatsApp { get; set; }
    public string? ContactEmail { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public CollectionPointModerationStatus ModerationStatus { get; set; } = CollectionPointModerationStatus.Pending;
    public CollectionPointOperationalStatus OperationalStatus { get; set; } = CollectionPointOperationalStatus.Open;
    public CollectionPointSource Source { get; set; } = CollectionPointSource.Community;
    public string? ManagementCodeHash { get; set; }
    public DateTimeOffset? ModeratedAt { get; set; }
    public string? ModeratedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<CollectionPointComment> Comments { get; set; } = [];
    public List<CollectionPointAbuseReport> AbuseReports { get; set; } = [];
}
