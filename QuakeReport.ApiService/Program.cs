using FluentValidation;
using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.BloodDonationCenters;
using QuakeReport.ApiService.CollectionPoints;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.GeocodingReview;
using QuakeReport.ApiService.HelpRequests;
using QuakeReport.ApiService.Ingestion;
using QuakeReport.ApiService.Media;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.ApiService.Persistence;
using QuakeReport.ApiService.Reports;
using QuakeReport.ApiService.Security;
using QuakeReport.ApiService.Shelters;
using QuakeReport.ApiService.Validation;
using QuakeReport.Core.Models.API;
using QuakeReport.Data;
using QuakeReport.Data.Geospatial;
using QuakeReport.Data.Models;
using QuakeReport.Geospatial;
using Scalar.AspNetCore;
using StorageGenerics.Core.Contracts;
using StorageGenerics.Services;
using System.Security.Cryptography;
using System.Text;

PostGisTypeMapping.Configure();
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("ingestion", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var supplied = context.Request.Headers["X-Ingestion-Api-Key"].ToString();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            $"{ip}:{fingerprint}",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddAzureNpgsqlDbContext<QuakeReportDbContext>("quakereportdb",
    configureDbContextOptions: options => options.UseNpgsql(npgsql => npgsql.UseNetTopologySuite()));
builder.Services.AddSingleton<IDbContextFactory<QuakeReportDbContext>, RuntimeQuakeReportDbContextFactory>();
AddQueryableRepository<BloodDonationCenter>(builder.Services);
AddQueryableRepository<BloodDonationCenterComment>(builder.Services);
AddQueryableRepository<CollectionPoint>(builder.Services);
AddQueryableRepository<CollectionPointComment>(builder.Services);
AddQueryableRepository<DamageReport>(builder.Services);
AddQueryableRepository<HelpRequest>(builder.Services);
AddQueryableRepository<HelpRequestComment>(builder.Services);
AddQueryableRepository<MissingPerson>(builder.Services);
AddQueryableRepository<MissingPersonTip>(builder.Services);
AddQueryableRepository<Shelter>(builder.Services);

builder.Services.AddScoped<IValidator<PaginationRequest>, PaginationRequestValidator>();
builder.Services.AddScoped<IValidator<GeoPointQuery>, GeoPointQueryValidator>();
builder.Services.AddScoped<
    IValidator<BloodDonationCenterSearchFilter>,
    BloodDonationCenterSearchFilterValidator>();
builder.Services.AddScoped<
    IValidator<CollectionPointSearchFilter>,
    CollectionPointSearchFilterValidator>();
builder.Services.AddScoped<
    IValidator<ShelterSearchFilter>,
    ShelterSearchFilterValidator>();
builder.Services.AddScoped<
    IValidator<HelpRequestSearchFilter>,
    HelpRequestSearchFilterValidator>();
builder.Services.AddScoped<
    IValidator<MissingPersonSearchFilter>,
    MissingPersonSearchFilterValidator>();
builder.Services.AddScoped<
    IValidator<DamageReportSearchFilter>,
    DamageReportSearchFilterValidator>();
builder.Services.AddScoped<
    IValidator<PagedRequest<BloodDonationCenterSearchFilter>>,
    BloodDonationCenterSearchRequestValidator>();
builder.Services.AddScoped<
    IValidator<PagedRequest<CollectionPointSearchFilter>>,
    CollectionPointSearchRequestValidator>();
builder.Services.AddScoped<
    IValidator<PagedRequest<ShelterSearchFilter>>,
    ShelterSearchRequestValidator>();
builder.Services.AddScoped<
    IValidator<PagedRequest<HelpRequestSearchFilter>>,
    HelpRequestSearchRequestValidator>();
builder.Services.AddScoped<
    IValidator<PagedRequest<MissingPersonSearchFilter>>,
    MissingPersonSearchRequestValidator>();
builder.Services.AddScoped<
    IValidator<PagedRequest<DamageReportSearchFilter>>,
    DamageReportSearchRequestValidator>();

builder.AddAzureBlobServiceClient("blobs");
builder.Services.AddHttpClient("turnstile", client =>
{
    client.BaseAddress = new Uri("https://challenges.cloudflare.com/turnstile/v0/");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<IActiveEarthquakeService, ActiveEarthquakeService>();
builder.Services.AddScoped<IBloodDonationCenterService, BloodDonationCenterService>();
builder.Services.AddScoped<ICollectionPointService, CollectionPointService>();
builder.Services.AddScoped<IHelpRequestService, HelpRequestService>();
builder.Services.AddScoped<IMissingPersonService, MissingPersonService>();
builder.Services.AddScoped<IDamageReportService, DamageReportService>();
builder.Services.AddScoped<IShelterService, ShelterService>();
builder.Services.AddScoped<IIngestionPersistenceService, IngestionPersistenceService>();
builder.Services.AddScoped<IGeocodingReviewService, GeocodingReviewService>();
builder.Services.AddScoped<IMediaStorage, AzureBlobMediaStorage>();
builder.Services.AddScoped<MissingPersonSecurity>();
builder.Services.AddScoped<ITurnstileValidator, TurnstileValidator>();
builder.Services.AddScoped<IMissingPersonPhotoStorage, MissingPersonPhotoStorage>();
builder.Services.AddSingleton<IIngestionApiKeyValidator, IngestionApiKeyValidator>();
builder.Services.AddSingleton<IModerationKeyValidator, ModerationKeyValidator>();
builder.Services.AddHttpClient<IGoogleGeocoder, GoogleGeocoder>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<GeocodingCoordinator>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Exposed in every environment (including production) so every endpoint is
// always discoverable at /scalar - there's no auth on this API yet either,
// so this isn't leaking anything a client couldn't already infer from calling it.
app.MapOpenApi();
app.MapScalarApiReference();

app.UseHttpsRedirection();
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();

static void AddQueryableRepository<TEntity>(IServiceCollection services)
    where TEntity : class, IEntity<Guid>
{
    services.AddScoped<IQueryableRepositoryService<TEntity, Guid>,
        QueryableRepositoryService<QuakeReportDbContext, TEntity, Guid>>();
}
