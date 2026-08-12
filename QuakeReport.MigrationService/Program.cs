using QuakeReport.Data;
using QuakeReport.MigrationService;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddAzureNpgsqlDbContext<QuakeReportDbContext>("quakereportdb",
    configureDbContextOptions: options => options.UseNpgsql(npgsql => npgsql.UseNetTopologySuite()));

var host = builder.Build();
host.Run();
