using Microsoft.Extensions.Logging;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Integration")]
public class WebTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [TestMethod]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource(DefaultTimeout).Token;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.QuakeReport_AppHost>(cancellationToken);
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            // Override the logging filters from the app's configuration
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        // Act
        var httpClient = app.CreateHttpClient("webfrontend");
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        var response = await httpClient.GetAsync("/", cancellationToken);

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task IngestionRelayForwardsJsonWithCharsetToInternalApi()
    {
        var cancellationToken = new CancellationTokenSource(DefaultTimeout).Token;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.QuakeReport_AppHost>(cancellationToken);
        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        var httpClient = app.CreateHttpClient("webfrontend");
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/v1/blood-donation-centers")
        {
            Content = new StringContent(
                """
                {
                  "source": {
                    "platform": 0,
                    "sourceUrl": "https://example.com/source",
                    "confidence": 0.5
                  },
                  "data": {
                    "name": "Centro de prueba",
                    "address": "Calle 1, Cali",
                    "operatingInstructions": "Confirmar directamente con el centro.",
                    "needsSummary": "Donantes",
                    "publicPhone": "+57 300 000 0000",
                    "centerType": 0,
                    "bloodTypes": 1,
                    "components": 1
                  }
                }
                """, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Ingestion-Api-Key", "invalid-test-key");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await httpClient.SendAsync(request, cancellationToken);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
