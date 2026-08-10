using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using QuakeReport.Data;

namespace QuakeReport.MigrationService;

/// <summary>
/// Runs on startup, applies any pending EF Core migrations to the QuakeReport
/// database (including seed data configured via HasData), then stops itself.
/// This is a one-shot job, not a long-running service.
/// </summary>
public class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    public const string ActivitySourceName = "QuakeReport.MigrationService";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = ActivitySource.StartActivity("Migrating database", ActivityKind.Client);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<QuakeReportDbContext>();

            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await dbContext.Database.MigrateAsync(stoppingToken);
            });
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            hostApplicationLifetime.StopApplication();
        }
    }
}
