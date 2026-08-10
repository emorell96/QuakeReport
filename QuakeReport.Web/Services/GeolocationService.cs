using Microsoft.JSInterop;

namespace QuakeReport.Web.Services;

public enum GeolocationFailureReason
{
    Denied,
    Timeout,
    Unavailable,
    Unsupported,
}

public abstract record GeolocationResult
{
    public sealed record Success(double Latitude, double Longitude) : GeolocationResult;

    public sealed record Failure(GeolocationFailureReason Reason) : GeolocationResult;
}

/// <summary>Wraps the browser Geolocation API via JS interop (wwwroot/js/geolocation.js).</summary>
public class GeolocationService(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> GetModuleAsync() =>
        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/geolocation.js");

    public async Task<GeolocationResult> GetCurrentPositionAsync()
    {
        try
        {
            var module = await GetModuleAsync();
            var position = await module.InvokeAsync<Position>("getCurrentPosition");
            return new GeolocationResult.Success(position.Latitude, position.Longitude);
        }
        catch (JSException ex)
        {
            var reason = ex.Message switch
            {
                var m when m.Contains("denied", StringComparison.OrdinalIgnoreCase) => GeolocationFailureReason.Denied,
                var m when m.Contains("timeout", StringComparison.OrdinalIgnoreCase) => GeolocationFailureReason.Timeout,
                var m when m.Contains("unsupported", StringComparison.OrdinalIgnoreCase) => GeolocationFailureReason.Unsupported,
                _ => GeolocationFailureReason.Unavailable,
            };
            return new GeolocationResult.Failure(reason);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    private record Position(double Latitude, double Longitude);
}
