using System.Net.Http.Json;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace QuakeReport.Geospatial;

public interface IGoogleGeocoder
{
    Task<GoogleGeocodingOutcome> GeocodeAsync(string address, CancellationToken cancellationToken = default);
}

public sealed record GoogleGeocodingCandidate(
    double Latitude, double Longitude, string? FormattedAddress, string? PlaceId,
    string? Granularity, bool PartialMatch);

public sealed record GoogleGeocodingOutcome(
    IReadOnlyList<GoogleGeocodingCandidate> Candidates, string? Error = null)
{
    public GoogleGeocodingCandidate? AutomaticMatch =>
        Candidates.Count == 1 && !Candidates[0].PartialMatch &&
        Candidates[0].Granularity is "ROOFTOP" or "RANGE_INTERPOLATED"
            ? Candidates[0]
            : null;
}

public sealed class GoogleGeocoder(HttpClient httpClient, IConfiguration configuration) : IGoogleGeocoder
{
    public async Task<GoogleGeocodingOutcome> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        var key = configuration["GoogleMaps:ApiKey"] ?? configuration["GOOGLE_MAPS_API_KEY"];
        if (string.IsNullOrWhiteSpace(key)) return new([], "Google Maps API key is not configured.");

        var uri = $"https://geocode.googleapis.com/v4/geocode/address/{Uri.EscapeDataString(address)}?key={Uri.EscapeDataString(key)}";
        string? lastError = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var response = await httpClient.GetAsync(uri, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadFromJsonAsync<GeocodeResponse>(cancellationToken);
                    return new(payload?.Results.Select(result => new GoogleGeocodingCandidate(
                        result.Location.Latitude, result.Location.Longitude, result.FormattedAddress,
                        result.PlaceId, result.Granularity, result.PartialMatch)).ToList() ?? []);
                }

                lastError = $"Google returned HTTP {(int)response.StatusCode}.";
                if (response.StatusCode != HttpStatusCode.TooManyRequests && (int)response.StatusCode < 500)
                    return new([], lastError);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException &&
                                               !cancellationToken.IsCancellationRequested)
            {
                lastError = exception.Message;
            }

            if (attempt < 3)
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken);
        }

        return new([], lastError ?? "Google geocoding failed after three attempts.");
    }

    private sealed record GeocodeResponse([property: JsonPropertyName("results")] IReadOnlyList<GeocodeResult> Results);
    private sealed record GeocodeResult(
        [property: JsonPropertyName("location")] GoogleLocation Location,
        [property: JsonPropertyName("formattedAddress")] string? FormattedAddress,
        [property: JsonPropertyName("placeId")] string? PlaceId,
        [property: JsonPropertyName("granularity")] string? Granularity,
        [property: JsonPropertyName("partialMatch")] bool PartialMatch);
    private sealed record GoogleLocation(
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);
}
