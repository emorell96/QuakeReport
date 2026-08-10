using System.Net.Http.Json;

namespace QuakeReport.ApiService.MissingPeople;

public interface ITurnstileValidator
{
    Task<TurnstileValidationResult> ValidateAsync(string? token, CancellationToken cancellationToken);
}

public record TurnstileValidationResult(bool Success, bool ProviderUnavailable = false);

public sealed class TurnstileValidator(IHttpClientFactory clients, IConfiguration configuration, IWebHostEnvironment environment) : ITurnstileValidator
{
    public async Task<TurnstileValidationResult> ValidateAsync(string? token, CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment() && configuration.GetValue("Turnstile:BypassInDevelopment", true))
        {
            return new(true);
        }

        if (string.IsNullOrWhiteSpace(token)) return new(false);
        var secret = configuration["Turnstile:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret)) return new(false, true);

        try
        {
            using var response = await clients.CreateClient("turnstile").PostAsJsonAsync(
                "siteverify", new { secret, response = token }, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, true);
            var result = await response.Content.ReadFromJsonAsync<Response>(cancellationToken);
            return new(result?.Success == true);
        }
        catch (HttpRequestException)
        {
            return new(false, true);
        }
    }

    private sealed record Response(bool Success);
}
