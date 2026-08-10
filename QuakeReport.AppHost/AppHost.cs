using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.PostgreSql;
using Azure.Provisioning.Storage;

var builder = DistributedApplication.CreateBuilder(args);

var existingEnvironmentName = builder.AddParameter(
    "existing-aca-environment-name",
    "quakereportenvyksjkeaewt",
    publishValueAsDefault: true);
var existingEnvironmentResourceGroup = builder.AddParameter(
    "existing-aca-environment-resource-group",
    "rg-terremoto-prod",
    publishValueAsDefault: true);

builder.AddAzureContainerAppEnvironment("quake-report-env")
    .AsExisting(existingEnvironmentName, existingEnvironmentResourceGroup);

var googleMapsApiKey = builder.AddParameter("google-maps-api-key", secret: true);
var apexDomain = builder.AddParameter(
    "apex-domain",
    "terremoto.com.co",
    publishValueAsDefault: true);
var wwwDomain = builder.AddParameter(
    "www-domain",
    "www.terremoto.com.co",
    publishValueAsDefault: true);
var cloudflareCertificateId = builder.AddParameter(
    "cloudflare-certificate-id",
    "/subscriptions/80fb3496-88b6-4097-86c6-efbc21c21cfa/resourceGroups/rg-terremoto-prod/providers/Microsoft.App/managedEnvironments/quakereportenvyksjkeaewt/certificates/terremoto-cloudflare",
    publishValueAsDefault: true);

var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
    .RunAsContainer(container => container
        .WithDataVolume()
        .WithLifetime(ContainerLifetime.Persistent))
    .ConfigureInfrastructure(infrastructure =>
    {
        var server = infrastructure.GetProvisionableResources()
            .OfType<PostgreSqlFlexibleServer>()
            .Single();

        server.Version = PostgreSqlFlexibleServerVersion.Sixteen;
        server.Sku = new PostgreSqlFlexibleServerSku
        {
            Name = "Standard_B1ms",
            Tier = PostgreSqlFlexibleServerSkuTier.Burstable,
        };
        server.Storage = new PostgreSqlFlexibleServerStorage
        {
            StorageSizeInGB = 32,
        };
        server.Backup = new PostgreSqlFlexibleServerBackupProperties
        {
            BackupRetentionDays = 7,
            GeoRedundantBackup = PostgreSqlFlexibleServerGeoRedundantBackupEnum.Disabled,
        };
        server.HighAvailability = new PostgreSqlFlexibleServerHighAvailability
        {
            Mode = PostgreSqlFlexibleServerHighAvailabilityMode.Disabled,
        };
    });

var quakeReportDb = postgres.AddDatabase("quakereportdb");

// Azurite is used only in run mode. Aspire publishes this resource as a real
// Azure Storage account and grants the API's managed identity blob access.
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .ConfigureInfrastructure(infrastructure =>
    {
        var account = infrastructure.GetProvisionableResources()
            .OfType<StorageAccount>()
            .Single();

        account.Sku = new StorageSku { Name = StorageSkuName.StandardLrs };
        account.AllowBlobPublicAccess = true;
    });
var reportMediaBlobs = storage.AddBlobs("blobs");

var migrationService = builder.AddProject<Projects.QuakeReport_MigrationService>("migrationservice")
    .WithReference(quakeReportDb)
    .WaitFor(quakeReportDb)
    .PublishAsAzureContainerAppJob();

var apiService = builder.AddProject<Projects.QuakeReport_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(quakeReportDb)
    .WaitFor(quakeReportDb)
    .WaitForCompletion(migrationService)
    .WithReference(reportMediaBlobs)
    .WaitFor(reportMediaBlobs)
    .PublishAsAzureContainerApp((_, app) =>
    {
        app.Template.Scale.MinReplicas = 0;
        app.Template.Scale.MaxReplicas = 3;
    });

// Azure Container Apps automatically configures ASP.NET Core data protection.
// Give the Blazor frontend its own workload identity so that configuration does
// not depend on an identity being discovered from the not-yet-created app.
var webFrontendIdentity = builder.AddAzureUserAssignedIdentity("webfrontend-identity");

var webFrontend = builder.AddProject<Projects.QuakeReport_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithAzureUserAssignedIdentity(webFrontendIdentity)
    .WithEnvironment("GoogleMaps__ApiKey", googleMapsApiKey)
    .PublishAsAzureContainerApp((infrastructure, app) =>
    {
        app.Template.Scale.MinReplicas = 1;
        app.Template.Scale.MaxReplicas = 1;

        // Keep the Cloudflare hostnames and their uploaded origin certificate
        // in the declarative model so subsequent deployments preserve them.
        var apexDomainParameter = apexDomain.AsProvisioningParameter(
            infrastructure,
            "apex_domain");
        var wwwDomainParameter = wwwDomain.AsProvisioningParameter(
            infrastructure,
            "www_domain");
        var certificateIdParameter = cloudflareCertificateId.AsProvisioningParameter(
            infrastructure,
            "cloudflare_certificate_id");

        app.Configuration.Ingress.CustomDomains.Add(new ContainerAppCustomDomain
        {
            Name = apexDomainParameter,
            BindingType = ContainerAppCustomDomainBindingType.SniEnabled,
            CertificateId = certificateIdParameter,
        });
        app.Configuration.Ingress.CustomDomains.Add(new ContainerAppCustomDomain
        {
            Name = wwwDomainParameter,
            BindingType = ContainerAppCustomDomainBindingType.SniEnabled,
            CertificateId = certificateIdParameter,
        });
    });

if (builder.ExecutionContext.IsPublishMode)
{
    migrationService.WithEnvironment("DOTNET_ENVIRONMENT", "Production");
    apiService.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production");
    webFrontend.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production");
}

builder.Build().Run();
