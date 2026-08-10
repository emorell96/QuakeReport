namespace QuakeReport.Data.Models;

/// <summary>
/// A seismic event. Not linked to <see cref="DamageReport"/> yet — the app
/// currently covers a single seeded event (the Colombia M7.4 quake) — but
/// kept as its own entity so linking reports to specific events later is
/// just a column add, not a remodel.
/// </summary>
public class Earthquake
{
    public Guid Id { get; set; }

    /// <summary>Display label, e.g. "M7.4 - Colombia".</summary>
    public required string Name { get; set; }

    public required double Magnitude { get; set; }

    public required DateTimeOffset OccurredAt { get; set; }

    public required double EpicenterLatitude { get; set; }

    public required double EpicenterLongitude { get; set; }

    /// <summary>Origin of the data, e.g. "USGS:us7000abcd". Null for manually-entered events.</summary>
    public string? Source { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
