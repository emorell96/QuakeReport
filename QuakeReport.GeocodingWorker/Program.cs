using Microsoft.EntityFrameworkCore;
using QuakeReport.Data;
using QuakeReport.GeocodingWorker;
using QuakeReport.Geospatial;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddAzureNpgsqlDbContext<QuakeReportDbContext>("quakereportdb",
    configureDbContextOptions: options => options.UseNpgsql(npgsql => npgsql.UseNetTopologySuite()));
builder.Services.AddHttpClient<IGoogleGeocoder, GoogleGeocoder>(client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<GeocodingCoordinator>();
builder.Services.AddHostedService<Worker>();
builder.Build().Run();
