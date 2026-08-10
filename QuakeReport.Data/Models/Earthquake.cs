namespace QuakeReport.Data.Models;

/// <summary>
/// A seismic event. Every <see cref="DamageReport"/> links to one via
/// <see cref="DamageReport.EarthquakeId"/>, which the API resolves server-side
/// from <see cref="IsActive"/> rather than accepting it from the client.
/// Exactly one row should have <see cref="IsActive"/> set at a time. The app
/// currently only ever seeds one (the Colombia M7.4 quake); switching to a
/// future quake is just flipping this flag, no code or schema change.
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

    /// <summary>The event new reports get attributed to. Exactly one row should be true.</summary>
    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
