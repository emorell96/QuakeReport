using System.Globalization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using MudBlazor.Services;
using QuakeReport.Web.Components;
using QuakeReport.Web.Infrastructure;
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
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddMudServices();
builder.Services.AddOutputCache();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var teamDomain = builder.Configuration["CloudflareAccess:TeamDomain"] ?? string.Empty;
        options.Authority = $"https://{teamDomain}";
        options.Audience = builder.Configuration["CloudflareAccess:Audience"];
        options.RequireHttpsMetadata = true;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Headers["Cf-Access-Jwt-Assertion"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(token) && context.Request.Cookies.TryGetValue("CF_Authorization", out var cookie)) token = cookie;
                context.Token = token;
                return Task.CompletedTask;
            },
        };
    });
var moderatorPolicy = builder.Services.AddAuthorizationBuilder().AddPolicy("Moderators", policy =>
    policy.RequireAssertion(context => builder.Environment.IsDevelopment() || context.User.Identity?.IsAuthenticated == true));

#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is the documented per-client override.
builder.Services.AddHttpClient<QuakeReportApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
        client.Timeout = TimeSpan.FromMinutes(15);
    })
    // BrowserFileStream is forward-only. The default resilience handler times
    // out each attempt after ten seconds and then retries the consumed stream.
    // Uploads and report-creation POSTs must be sent exactly once.
    .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

builder.Services.AddHttpClient("ingestion-relay", client =>
{
    client.BaseAddress = new("https+http://apiservice");
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddHttpClient<GooglePlacesService>(client =>
{
    client.BaseAddress = new("https://places.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<GeolocationService>();
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<ShareService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseMiddleware<CanonicalDomainRedirectMiddleware>();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Cloudflare Access protects the admin route at the edge. Validate the Access
// identity again for direct-origin requests, but do not require Access on the
// shared /_blazor circuit endpoint used by the public site.
app.Use(async (context, next) =>
{
    if (!app.Environment.IsDevelopment() &&
        (context.Request.Path.StartsWithSegments("/acopios/admin") ||
         context.Request.Path.StartsWithSegments("/refugios/admin") ||
         context.Request.Path.StartsWithSegments("/ayuda/admin") ||
         context.Request.Path.StartsWithSegments("/donacion-sangre/admin")) &&
        context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    await next();
});

app.UseAntiforgery();

app.UseOutputCache();

app.MapPost("/api/ingestion/v1/{kind}", async (string kind, HttpContext context, IHttpClientFactory clients, CancellationToken cancellationToken) =>
{
    var allowed = kind is "collection-points" or "blood-donation-centers" or "shelters" or "help-requests";
    if (!allowed) return Results.NotFound();

    using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/ingestion/v1/{kind}")
    {
        Content = new StreamContent(context.Request.Body)
    };
    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(context.Request.ContentType ?? "application/json");
    if (context.Request.Headers.TryGetValue("X-Ingestion-Api-Key", out var apiKey)) request.Headers.TryAddWithoutValidation("X-Ingestion-Api-Key", apiKey.ToArray());
    if (context.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey)) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey.ToArray());

    using var response = await clients.CreateClient("ingestion-relay").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    var body = await response.Content.ReadAsStringAsync(cancellationToken);
    return Results.Content(body, response.Content.Headers.ContentType?.ToString() ?? "application/json", statusCode: (int)response.StatusCode);
});

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
