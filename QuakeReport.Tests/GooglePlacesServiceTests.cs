using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QuakeReport.Web.Services;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class GooglePlacesServiceTests
{
    [TestMethod]
    public async Task SearchDoesNotCallGoogleBeforeFourCharacters()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var service = CreateService(handler);

        var result = await service.SearchAsync("abc", null, null, "session", CancellationToken.None);

        Assert.AreEqual(0, result.Count);
        Assert.AreEqual(0, handler.CallCount);
    }

    [TestMethod]
    public async Task SearchFailsBeforeCallingGoogleWhenApiKeyIsMissing()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://places.googleapis.com/"),
        };
        var configuration = new ConfigurationBuilder().Build();
        var service = new GooglePlacesService(
            client,
            configuration,
            NullLogger<GooglePlacesService>.Instance);

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.SearchAsync("Bogota", null, null, "session", CancellationToken.None));

        StringAssert.Contains(exception.Message, "Google Maps API key is not configured");
        Assert.AreEqual(0, handler.CallCount);
    }

    [TestMethod]
    public async Task SearchCallsPlacesApiWithServerKeyFieldMaskAndLocationBias()
    {
        const string response = """
            {
              "suggestions": [
                {
                  "placePrediction": {
                    "placeId": "place-123",
                    "text": { "text": "Nieuwstraat 7B, Amsterdam, Netherlands" }
                  }
                }
              ]
            }
            """;
        var handler = new RecordingHandler(_ => JsonResponse(response));
        var service = CreateService(handler);

        var result = await service.SearchAsync(
            "Nieuwstraat",
            52.1582,
            4.4928,
            "session-123",
            CancellationToken.None);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("place-123", result[0].PlaceId);
        Assert.AreEqual("Nieuwstraat 7B, Amsterdam, Netherlands", result[0].Description);
        Assert.AreEqual(HttpMethod.Post, handler.Method);
        Assert.AreEqual("https://places.googleapis.com/v1/places:autocomplete", handler.RequestUri?.ToString());
        Assert.AreEqual("test-api-key", handler.ApiKey);
        StringAssert.Contains(handler.FieldMask, "suggestions.placePrediction.placeId");
        StringAssert.Contains(handler.Body, "\"input\":\"Nieuwstraat\"");
        StringAssert.Contains(handler.Body, "\"sessionToken\":\"session-123\"");
        StringAssert.Contains(handler.Body, "\"latitude\":52.1582");
        StringAssert.Contains(handler.Body, "\"longitude\":4.4928");
    }

    [TestMethod]
    public async Task GetDetailsReturnsFormattedAddressAndCoordinates()
    {
        const string response = """
            {
              "formattedAddress": "Nieuwstraat 7B, Amsterdam, Netherlands",
              "location": { "latitude": 52.37, "longitude": 4.89 }
            }
            """;
        var handler = new RecordingHandler(_ => JsonResponse(response));
        var service = CreateService(handler);

        var result = await service.GetDetailsAsync("place-123", "session-123", CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual("Nieuwstraat 7B, Amsterdam, Netherlands", result!.FormattedAddress);
        Assert.AreEqual(52.37, result.Latitude);
        Assert.AreEqual(4.89, result.Longitude);
        Assert.AreEqual(
            "https://places.googleapis.com/v1/places/place-123?sessionToken=session-123",
            handler.RequestUri?.ToString());
        Assert.AreEqual("formattedAddress,location", handler.FieldMask);
    }

    [TestMethod]
    public async Task ReverseGeocodeUsesServerSideV4Endpoint()
    {
        const string response = """
            {
              "results": [
                { "formattedAddress": "Nieuwstraat 7B, Amsterdam, Netherlands" }
              ]
            }
            """;
        var handler = new RecordingHandler(_ => JsonResponse(response));
        var service = CreateService(handler);

        var result = await service.ReverseGeocodeAsync(52.1582, 4.4928);

        Assert.AreEqual("Nieuwstraat 7B, Amsterdam, Netherlands", result);
        Assert.AreEqual(
            "https://geocode.googleapis.com/v4/geocode/location/52.1582,4.4928",
            handler.RequestUri?.ToString());
        Assert.AreEqual("results.formattedAddress", handler.FieldMask);
        Assert.AreEqual("test-api-key", handler.ApiKey);
    }

    [TestMethod]
    public async Task ServerSideRequestsUsePrivateApiKeyWhenBothKeysAreConfigured()
    {
        var handler = new RecordingHandler(_ => JsonResponse("{}"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleMaps:ApiKey"] = "public-browser-key",
                ["GoogleMaps:PrivateApiKey"] = "private-server-key",
            })
            .Build();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://places.googleapis.com/"),
        };
        var service = new GooglePlacesService(
            client,
            configuration,
            NullLogger<GooglePlacesService>.Instance);

        await service.ReverseGeocodeAsync(52.1582, 4.4928);

        Assert.AreEqual("private-server-key", handler.ApiKey);
    }

    private static GooglePlacesService CreateService(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleMaps:PrivateApiKey"] = "test-api-key",
            })
            .Build();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://places.googleapis.com/"),
        };
        return new GooglePlacesService(client, configuration, NullLogger<GooglePlacesService>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }
        public string? FieldMask { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("X-Goog-Api-Key").Single();
            FieldMask = request.Headers.GetValues("X-Goog-FieldMask").Single();
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
