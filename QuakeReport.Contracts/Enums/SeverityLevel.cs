namespace QuakeReport.Contracts.Enums;

/// <summary>
/// Self-reported impact severity for a damage report.
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
