using System.Globalization;
using MudBlazor.Services;
using QuakeReport.Web.Components;
using QuakeReport.Web.Services;

// Site is Spanish-only (no locale switcher) - this drives date/number
// formatting (e.g. report timestamps) into Spanish as well as the UI text.
var spanishCulture = new CultureInfo("es-CO");
CultureInfo.DefaultThreadCurrentCulture = spanishCulture;
CultureInfo.DefaultThreadCurrentUICulture = spanishCulture;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddOutputCache();

builder.Services.AddHttpClient<QuakeReportApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

builder.Services.AddHttpClient<GooglePlacesService>(client =>
{
    client.BaseAddress = new("https://places.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<GeolocationService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
