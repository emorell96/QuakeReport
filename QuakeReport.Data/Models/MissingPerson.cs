using QuakeReport.Contracts.Enums;
using StorageGenerics.Core.Contracts;

namespace QuakeReport.Data.Models;

public class MissingPerson : IEntity<Guid>
{
    public Guid Id { get; set; }
    public required Guid EarthquakeId { get; set; }
    public Earthquake? Earthquake { get; set; }
    public required string FullName { get; set; }
    public string? SearchName { get; set; }
    public string? Aliases { get; set; }
    public string? ApproximateAge { get; set; }
    public IdentificationDocumentType? IdentificationDocumentType { get; set; }
    public string? IdentificationNumberHash { get; set; }
    public string? IdentificationLastFour { get; set; }
    public required string Description { get; set; }
    public string? PhysicalDescription { get; set; }
    public string? ClothingDescription { get; set; }
    public required DateTimeOffset LastSeenAt { get; set; }
    public MissingPersonStatus Status { get; set; } = MissingPersonStatus.Missing;
    public string? PhotoUrl { get; set; }
    public required string ManagementCodeHash { get; set; }
    public DateTimeOffset PublicationConsentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<MissingPersonLocation> Locations { get; set; } = [];
    public List<MissingPersonTip> Tips { get; set; } = [];
    public List<AbuseReport> AbuseReports { get; set; } = [];
}
