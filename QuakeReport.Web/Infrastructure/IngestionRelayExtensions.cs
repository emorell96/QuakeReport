using System.Net.Http.Headers;

namespace QuakeReport.Web.Infrastructure;

public static class IngestionRelayExtensions
{
    private static readonly HashSet<string> AllowedKinds =
    [
        "collection-points",
        "blood-donation-centers",
        "shelters",
        "help-requests"
    ];

    public static IEndpointRouteBuilder MapIngestionRelay(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/ingestion/v1/{kind}", RelayAsync);
        return endpoints;
    }

    private static async Task<IResult> RelayAsync(
        string kind,
        HttpContext context,
        IHttpClientFactory clients,
        CancellationToken cancellationToken)
    {
        if (!AllowedKinds.Contains(kind)) return Results.NotFound();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/ingestion/v1/{kind}")
        {
            Content = new StreamContent(context.Request.Body)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(
            context.Request.ContentType ?? "application/json");

        ForwardHeader(context, request, "X-Ingestion-Api-Key");
        ForwardHeader(context, request, "Idempotency-Key");

        using var response = await clients.CreateClient("ingestion-relay")
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return Results.Content(
            body,
            response.Content.Headers.ContentType?.ToString() ?? "application/json",
            statusCode: (int)response.StatusCode);
    }

    private static void ForwardHeader(HttpContext context, HttpRequestMessage request, string name)
    {
        if (context.Request.Headers.TryGetValue(name, out var values))
            request.Headers.TryAddWithoutValidation(name, values.ToArray());
    }
}
