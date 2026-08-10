using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Media;
using QuakeReport.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddAzureNpgsqlDbContext<QuakeReportDbContext>("quakereportdb");
builder.AddAzureBlobServiceClient("blobs");

builder.Services.AddScoped<ActiveEarthquakeService>();
builder.Services.AddScoped<IMediaStorage, AzureBlobMediaStorage>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Exposed in every environment (including production) so every endpoint is
// always discoverable at /scalar - there's no auth on this API yet either,
// so this isn't leaking anything a client couldn't already infer from calling it.
app.MapOpenApi();
app.MapScalarApiReference();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
