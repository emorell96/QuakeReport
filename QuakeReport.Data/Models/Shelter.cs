using QuakeReport.Contracts.Enums;
using NetTopologySuite.Geometries;

namespace QuakeReport.Data.Models;

public class Shelter : IGeocodableEntity
{
    public Guid Id { get; set; }
    public required Guid EarthquakeId { get; set; }
    public Earthquake? Earthquake { get; set; }
    public required string Name { get; set; }
    public string? OrganizationName { get; set; }
    public required string Address { get; set; }
    public string? SearchText { get; set; }
    public Point? Location { get; set; }
    public required string Description { get; set; }
    public required string OperatingInstructions { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactWhatsApp { get; set; }
    public string? ContactEmail { get; set; }
    public ShelterModerationStatus ModerationStatus { get; set; } = ShelterModerationStatus.Pending;
    public ShelterOperationalStatus OperationalStatus { get; set; } = ShelterOperationalStatus.Open;
    public ShelterSource Source { get; set; } = ShelterSource.Community;
    public string? ManagementCodeHash { get; set; }
    public DateTimeOffset? ModeratedAt { get; set; }
    public string? ModeratedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ShelterAbuseReport> AbuseReports { get; set; } = [];
}
