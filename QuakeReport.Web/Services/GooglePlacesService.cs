using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace QuakeReport.Web.Services;

public sealed record GooglePlaceSuggestion(string PlaceId, string Description);

public sealed record GooglePlaceDetails(string FormattedAddress, double Latitude, double Longitude);

/// <summary>
/// Calls Google Places and Geocoding from the server so the API key is never
/// sent to the browser. The UI also enforces this service's four-character minimum.
/// </summary>
public sealed class GooglePlacesService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GooglePlacesService> logger)
{
    public const int MinimumSearchLength = 4;

    public async Task<IReadOnlyList<GooglePlaceSuggestion>> SearchAsync(
        string? query,
        double? latitude,
        double? longitude,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        query = query?.Trim();
        if (string.IsNullOrEmpty(query) || query.Length < MinimumSearchLength)
        {
            return [];
        }

        object payload = latitude.HasValue && longitude.HasValue
            ? new
            {
                input = query,
                sessionToken,
                locationBias = new
                {
                    circle = new
                    {
                        center = new { latitude = latitude.Value, longitude = longitude.Value },
                        radius = 50_000.0,
                    },
                },
            }
            : new { input = query, sessionToken };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/places:autocomplete")
        {
            Content = JsonContent.Create(payload),
        };
        AddGoogleHeaders(
            request,
            "suggestions.placePrediction.placeId,suggestions.placePrediction.text.text");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Google Places autocomplete returned HTTP {StatusCode}.",
                (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<AutocompleteResponse>(cancellationToken);
        return result?.Suggestions
            .Select(suggestion => suggestion.PlacePrediction)
            .Where(prediction =>
                !string.IsNullOrWhiteSpace(prediction?.PlaceId)
                && !string.IsNullOrWhiteSpace(prediction.Text?.Value))
            .Select(prediction => new GooglePlaceSuggestion(prediction!.PlaceId!, prediction.Text!.Value!))
            .ToList()
            ?? [];
    }

    public async Task<GooglePlaceDetails?> GetDetailsAsync(
        string placeId,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        var path = $"v1/places/{Uri.EscapeDataString(placeId)}?sessionToken={Uri.EscapeDataString(sessionToken)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        AddGoogleHeaders(request, "formattedAddress,location");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Google Place Details returned HTTP {StatusCode}.",
                (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        var place = await response.Content.ReadFromJsonAsync<PlaceDetailsResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(place?.FormattedAddress) || place.Location is null)
        {
            return null;
        }

        return new GooglePlaceDetails(
            place.FormattedAddress,
            place.Location.Latitude,
            place.Location.Longitude);
    }

    public async Task<string?> ReverseGeocodeAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        var coordinates = string.Create(
            CultureInfo.InvariantCulture,
            $"{latitude},{longitude}");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://geocode.googleapis.com/v4/geocode/location/{coordinates}");
        AddGoogleHeaders(request, "results.formattedAddress");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Google reverse geocoding returned HTTP {StatusCode}.",
                (int)response.StatusCode);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<GeocodeResponse>(cancellationToken);
        return result?.Results.FirstOrDefault()?.FormattedAddress;
    }

    private void AddGoogleHeaders(HttpRequestMessage request, string fieldMask)
    {
        var apiKey = configuration["GoogleMaps:PrivateApiKey"]
            ?? configuration["GOOGLE_MAPS_PRIVATE_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Google Maps API key is not configured. Set the GoogleMaps:PrivateApiKey or GOOGLE_MAPS_PRIVATE_API_KEY user secret.");
        }

        request.Headers.Add("X-Goog-Api-Key", apiKey);
        request.Headers.Add("X-Goog-FieldMask", fieldMask);
    }

    private sealed record AutocompleteResponse(
        [property: JsonPropertyName("suggestions")] IReadOnlyList<AutocompleteSuggestion> Suggestions)
    {
        public AutocompleteResponse() : this([]) { }
    }

    private sealed record AutocompleteSuggestion(
        [property: JsonPropertyName("placePrediction")] PlacePrediction? PlacePrediction);

    private sealed record PlacePrediction(
        [property: JsonPropertyName("placeId")] string? PlaceId,
        [property: JsonPropertyName("text")] GoogleText? Text);

    private sealed record GoogleText(
        [property: JsonPropertyName("text")] string? Value);

    private sealed record PlaceDetailsResponse(
        [property: JsonPropertyName("formattedAddress")] string? FormattedAddress,
        [property: JsonPropertyName("location")] GoogleLocation? Location);

    private sealed record GoogleLocation(
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);

    private sealed record GeocodeResponse(
        [property: JsonPropertyName("results")] IReadOnlyList<GeocodeResult> Results)
    {
        public GeocodeResponse() : this([]) { }
    }

    private sealed record GeocodeResult(
        [property: JsonPropertyName("formattedAddress")] string? FormattedAddress);
}
