using Microsoft.EntityFrameworkCore;
using QuakeReport.Data;
using QuakeReport.Data.Models;

namespace QuakeReport.ApiService.Earthquakes;

public interface IActiveEarthquakeService
{
    Task<Earthquake?> GetActiveEarthquakeAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the earthquake new reports get attributed to. Today there's
/// exactly one active row (the seeded Colombia M7.4 quake); when a future
/// quake needs its own reports, flip Earthquake.IsActive - no code change.
/// </summary>
public sealed class ActiveEarthquakeService(QuakeReportDbContext dbContext) : IActiveEarthquakeService
{
    public async Task<Earthquake?> GetActiveEarthquakeAsync(CancellationToken cancellationToken) =>
        await dbContext.Earthquakes.SingleOrDefaultAsync(e => e.IsActive, cancellationToken);
}
