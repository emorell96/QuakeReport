var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var quakeReportDb = postgres.AddDatabase("quakereportdb");

var migrationService = builder.AddProject<Projects.QuakeReport_MigrationService>("migrationservice")
    .WithReference(quakeReportDb)
    .WaitFor(quakeReportDb);

var apiService = builder.AddProject<Projects.QuakeReport_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(quakeReportDb)
    .WaitFor(quakeReportDb)
    .WaitForCompletion(migrationService);

builder.AddProject<Projects.QuakeReport_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
