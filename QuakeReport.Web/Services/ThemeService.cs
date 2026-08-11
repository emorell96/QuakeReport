using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace QuakeReport.Web.Services;

public interface IThemeService
{
    List<EventCallback<Theme>> ThemeChangedCallbacks { get; }

    Task SetCurrentThemeAsync(Theme theme);
    Task<Theme> GetCurrentThemeAsync();
}

public enum Theme
{
    Dark,
    Light,
}

public sealed class ThemeService : IThemeService, IDisposable
{
    private const string StorageKey = "terremoto-theme";
    private readonly IJSRuntime _jsRuntime;
    private readonly DotNetObjectReference<ThemeService> _dotNetRef;

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
        _dotNetRef = DotNetObjectReference.Create(this);
    }

    public List<EventCallback<Theme>> ThemeChangedCallbacks { get; } = [];

    public async Task SetCurrentThemeAsync(Theme theme)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, theme.ToString());

        var callbacks = ThemeChangedCallbacks
            .Select(callback => callback.InvokeAsync(theme))
            .ToList();

        await Task.WhenAll(callbacks);
    }

    public async Task<Theme> GetCurrentThemeAsync()
    {
        var savedTheme = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (Enum.TryParse<Theme>(savedTheme, ignoreCase: true, out var theme))
        {
            return theme;
        }

        var isDark = await _jsRuntime.InvokeAsync<bool>("mudThemeProvider.isDarkMode", _dotNetRef);
        return isDark ? Theme.Dark : Theme.Light;
    }

    public void Dispose() => _dotNetRef.Dispose();
}
