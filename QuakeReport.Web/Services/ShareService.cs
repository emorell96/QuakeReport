using Microsoft.JSInterop;

namespace QuakeReport.Web.Services;

public enum ShareResult { Shared, Copied, Cancelled, Failed }

public sealed class ShareService(IJSRuntime jsRuntime)
{
    public async Task<ShareResult> ShareAsync(string title, string text, string url, CancellationToken cancellationToken = default)
    {
        var result = await jsRuntime.InvokeAsync<string>("helpSharing.share", cancellationToken, new { title, text, url });
        return Enum.TryParse<ShareResult>(result, true, out var parsed) ? parsed : ShareResult.Failed;
    }
}
