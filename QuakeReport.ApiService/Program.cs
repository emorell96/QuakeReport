using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Media;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.Data;
using Scalar.AspNetCore;

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
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddAzureNpgsqlDbContext<QuakeReportDbContext>("quakereportdb");
builder.AddAzureBlobServiceClient("blobs");
builder.Services.AddHttpClient("turnstile", client =>
{
    client.BaseAddress = new Uri("https://challenges.cloudflare.com/turnstile/v0/");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<ActiveEarthquakeService>();
builder.Services.AddScoped<IMediaStorage, AzureBlobMediaStorage>();
builder.Services.AddScoped<MissingPersonSecurity>();
builder.Services.AddScoped<ITurnstileValidator, TurnstileValidator>();
builder.Services.AddScoped<IMissingPersonPhotoStorage, MissingPersonPhotoStorage>();

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
