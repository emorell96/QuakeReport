using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning.AppContainers;
using Azure.Provisioning.PostgreSql;
using Azure.Provisioning.Storage;

var builder = DistributedApplication.CreateBuilder(args);

// Example: quakereportenvyksjkeaewt
var existingEnvironmentName = builder.AddParameter("existing-aca-environment-name", "quakereportenvyksjkeaewt", publishValueAsDefault: true);
// Example: rg-terremoto-prod
var existingEnvironmentResourceGroup = builder.AddParameter(
    "existing-aca-environment-resource-group",
    "rg-terremoto-prod",
    publishValueAsDefault: true);

builder.AddAzureContainerAppEnvironment("quake-report-env")
    .AsExisting(existingEnvironmentName, existingEnvironmentResourceGroup);

// Example shape: AIzaSy... (Google Maps Platform API key)
var googleMapsApiKey = builder.AddParameter("google-maps-api-key", secret: true);
// Example shape: a long random value such as development-only-missing-person-id-hmac-key
var missingPersonIdHmacKey = builder.AddParameter("missing-person-id-hmac-key", secret: true);
// Example/test shape: 1x00000000000000000000AA
var turnstileSiteKey = builder.AddParameter("turnstile-site-key");
// Example/test shape: 1x0000000000000000000000000000000AA
var turnstileSecretKey = builder.AddParameter("turnstile-secret-key", secret: true);
// Example shape: a long random value such as development-only-moderation-api-key
var moderationApiKey = builder.AddParameter("moderation-api-key", secret: true);
// Example shape: a random 32-byte Base64 or hexadecimal value
var ingestionApiKey = builder.AddParameter("ingestion-api-key", secret: true);
// Example: development.cloudflareaccess.com (do not include https:// or a trailing slash)
var cloudflareAccessTeamDomain = builder.AddParameter("cloudflare-access-team-domain");
// Example shape: development-audience
var cloudflareAccessAudience = builder.AddParameter("cloudflare-access-audience");
// Example: terremoto.com.co
var apexDomain = builder.AddParameter("apex-domain", "terremoto.com.co", publishValueAsDefault: true);
// Example: www.terremoto.com.co
var wwwDomain = builder.AddParameter("www-domain", "www.terremoto.com.co", publishValueAsDefault: true);
// Example: /subscriptions/<subscription-id>/resourceGroups/rg-terremoto-prod/providers/Microsoft.App/managedEnvironments/quakereportenvyksjkeaewt/certificates/terremoto-cloudflare
var cloudflareCertificateId = builder.AddParameter(
    "cloudflare-certificate-id",
    "/subscriptions/80fb3496-88b6-4097-86c6-efbc21c21cfa/resourceGroups/rg-terremoto-prod/providers/Microsoft.App/managedEnvironments/quakereportenvyksjkeaewt/certificates/terremoto-cloudflare",
    publishValueAsDefault: true);

var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
    .RunAsContainer(container => container
        .WithImage("postgis/postgis", "17-3.6-alpine")
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

        infrastructure.Add(new PostgreSqlFlexibleServerConfiguration("postgres_postgis_extension", "2024-08-01")
        {
            Parent = server,
            Name = "azure.extensions",
            Source = "user-override",
            Value = "postgis",
        });
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
var missingPersonBlobs = storage.AddBlobs("missing-person-media");

// Jobs cannot use their own system-assigned identity while they are being
// created: Azure attempts to resolve that principal before the job exists.
// Stable user-assigned identities remove that circular provisioning dependency.
var migrationServiceIdentity = builder.AddAzureUserAssignedIdentity("migrationservice-identity");
var migrationService = builder.AddProject<Projects.QuakeReport_MigrationService>("migrationservice")
    .WithReference(quakeReportDb)
    .WaitFor(quakeReportDb)
    .WithAzureUserAssignedIdentity(migrationServiceIdentity)
    .PublishAsAzureContainerAppJob();

var apiService = builder.AddProject<Projects.QuakeReport_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(quakeReportDb)
    .WaitFor(quakeReportDb)
    .WaitForCompletion(migrationService)
    .WithReference(reportMediaBlobs)
    .WithReference(missingPersonBlobs)
    .WaitFor(reportMediaBlobs)
    .WithEnvironment("MissingPeople__IdHmacKey", missingPersonIdHmacKey)
    .WithEnvironment("Turnstile__SecretKey", turnstileSecretKey)
    .WithEnvironment("Moderation__ApiKey", moderationApiKey)
    .WithEnvironment("Ingestion__ApiKey", ingestionApiKey)
    .WithEnvironment("GoogleMaps__ApiKey", googleMapsApiKey)
    .PublishAsAzureContainerApp((_, app) =>
    {
        app.Template.Scale.MinReplicas = 0;
        app.Template.Scale.MaxReplicas = 3;
    });

var geocodingWorkerIdentity = builder.AddAzureUserAssignedIdentity("geocodingworker-identity");
var geocodingWorker = builder.AddProject<Projects.QuakeReport_GeocodingWorker>("geocodingworker")
    .WithReference(quakeReportDb)
    .WaitFor(quakeReportDb)
    .WaitForCompletion(migrationService)
    .WithEnvironment("GoogleMaps__ApiKey", googleMapsApiKey)
    .WithAzureUserAssignedIdentity(geocodingWorkerIdentity)
    .WithExplicitStart()
    .PublishAsAzureContainerAppJob();

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
    .WithEnvironment("Turnstile__SiteKey", turnstileSiteKey)
    .WithEnvironment("Moderation__ApiKey", moderationApiKey)
    .WithEnvironment("CloudflareAccess__TeamDomain", cloudflareAccessTeamDomain)
    .WithEnvironment("CloudflareAccess__Audience", cloudflareAccessAudience)
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
    geocodingWorker.WithEnvironment("DOTNET_ENVIRONMENT", "Production");
    webFrontend.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production");
}

builder.Build().Run();
