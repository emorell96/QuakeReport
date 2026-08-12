using System.Diagnostics;
using QuakeReport.Geospatial;

namespace QuakeReport.GeocodingWorker;

public sealed class Worker(
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    ILogger<Worker> logger) : BackgroundService
{
    public const string ActivitySourceName = "QuakeReport.GeocodingWorker";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = ActivitySource.StartActivity("Geocode missing locations", ActivityKind.Client);
        try
        {
            await using var scope = services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<GeocodingCoordinator>().RunAsync(stoppingToken);
            logger.LogInformation("Geocoding completed: examined {Examined}, located {Located}, queued {Queued}, skipped {Skipped}.",
                result.Examined, result.Located, result.Queued, result.Skipped);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            logger.LogError(exception, "Geocoding job failed.");
            Environment.ExitCode = 1;
        }
        finally { lifetime.StopApplication(); }
    }
}
