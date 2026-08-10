namespace QuakeReport.Data.Models;

/// <summary>
/// Self-reported impact severity for a <see cref="DamageReport"/>.
/// Backed by explicit int values so sorting worst-to-least-impact
/// (ORDER BY Severity DESC) stays stable even if members are reordered.
/// </summary>
public enum SeverityLevel
{
    Minor = 1,
    Moderate = 2,
    Major = 3,
    Severe = 4,
    Catastrophic = 5
}
