using QuakeReport.Contracts.Enums;

namespace QuakeReport.Data.Models;

/// <summary>
/// A citizen-submitted damage report. Anonymous - no reporter identity is captured.
/// </summary>
public class DamageReport
{
    public Guid Id { get; set; }

    /// <summary>Resolved server-side from the active <see cref="Earthquake"/> - never client-supplied.</summary>
    public required Guid EarthquakeId { get; set; }

    public Earthquake? Earthquake { get; set; }

    public required string Description { get; set; }

    /// <summary>Self-reported worst-to-least-impact sort key.</summary>
    public required SeverityLevel Severity { get; set; }

    public DamageSign DamageSigns { get; set; } = DamageSign.None;

    /// <summary>Optional - not every report involves a structure.</summary>
    public StructureType? StructureType { get; set; }

    /// <summary>Optional - rough proxy for how many people are affected.</summary>
    public StructureSize? StructureSize { get; set; }

    public required double Latitude { get; set; }

    public required double Longitude { get; set; }

    /// <summary>Optional free-text or reverse-geocoded address.</summary>
    public string? Address { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ReportMedia> Media { get; set; } = [];
}
