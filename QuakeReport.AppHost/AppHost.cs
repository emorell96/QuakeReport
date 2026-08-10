using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

// Persistent: the container survives AppHost stop/restart (across dotnet run
// and F5 sessions) instead of being torn down and recreated each time, which
// keeps it paired with its data volume - it just gets reattached, not reinitialized.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin();

var quakeReportDb = postgres.AddDatabase("quakereportdb");

// Local dev runs against the Azurite emulator; point ConnectionStrings__blobs
// at a real Azure Storage account connection string in prod - no code change needed.
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var reportMediaBlobs = storage.AddBlobs("blobs");

var migrationService = builder.AddProject<Projects.QuakeReport_MigrationService>("migrationservice")
    .WithReference(quakeReportDb)
    .WaitFor(quakeReportDb);

var apiService = builder.AddProject<Projects.QuakeReport_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(quakeReportDb)
    .WaitFor(quakeReportDb)
    .WaitForCompletion(migrationService)
    .WithReference(reportMediaBlobs)
    .WaitFor(reportMediaBlobs);

builder.AddProject<Projects.QuakeReport_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
