using QuakeReport.Data;
using QuakeReport.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddNpgsqlDbContext<QuakeReportDbContext>("quakereportdb");

var host = builder.Build();
host.Run();
