using System.Text.Encodings.Web;
using System.Text;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;

namespace QuakeReport.Web.Helpers;

public sealed record MapElementPresentation(
    string Label,
    string Glyph,
    string Color,
    string? DetailPath);

public static class MapPresentation
{
    public static readonly IReadOnlyList<MapElementType> LayerTypes =
    [
        MapElementType.DamageReport,
        MapElementType.Shelter,
        MapElementType.CollectionPoint,
        MapElementType.BloodDonationCenter,
        MapElementType.HelpRequest,
        MapElementType.MissingPerson,
    ];

    public static MapElementPresentation For(MapElementResponse element) =>
        element.Type switch
        {
            MapElementType.Earthquake => new("Epicentro", "E", "#7b1fa2", null),
            MapElementType.DamageReport => new(
                "Reporte de daño",
                "D",
                "#d32f2f",
                $"/report/{element.EntityId}"),
            MapElementType.Shelter => new(
                "Refugio",
                "R",
                "#1976d2",
                $"/refugios/{element.EntityId}"),
            MapElementType.CollectionPoint => new(
                "Centro de acopio",
                "A",
                "#388e3c",
                $"/collection-points/{element.EntityId}"),
            MapElementType.BloodDonationCenter => new(
                "Donación de sangre",
                "S",
                "#c2185b",
                $"/donacion-sangre/{element.EntityId}"),
            MapElementType.HelpRequest => new(
                "Solicitud de ayuda",
                "H",
                "#f57c00",
                $"/ayuda/{element.EntityId}"),
            MapElementType.MissingPerson => new(
                "Persona extraviada",
                "P",
                "#455a64",
                $"/missing-people/{element.EntityId}"),
            _ => throw new ArgumentOutOfRangeException(nameof(element.Type)),
        };

    public static IEnumerable<MapElementResponse> VisibleElements(
        IEnumerable<MapElementResponse> elements,
        IReadOnlySet<MapElementType> visibleLayers) =>
        elements.Where(element =>
            element.Type == MapElementType.Earthquake ||
            visibleLayers.Contains(element.Type));

    public static string MarkerIconDataUrl(MapElementResponse element)
    {
        var presentation = For(element);
        var size = element.Type == MapElementType.Earthquake ? 52 : 44;
        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 44 52">
                <path d="M22 1C10.4 1 1 10.4 1 22c0 15.8 21 29 21 29s21-13.2 21-29C43 10.4 33.6 1 22 1Z"
                      fill="{presentation.Color}" stroke="#ffffff" stroke-width="2" />
                <circle cx="22" cy="22" r="12" fill="rgba(255,255,255,0.16)" />
                <text x="22" y="27" text-anchor="middle" fill="#ffffff"
                      font-family="Arial, sans-serif" font-size="15" font-weight="700">
                    {presentation.Glyph}
                </text>
            </svg>
            """;

        return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}";
    }

    public static string InfoWindowHtml(MapElementResponse element)
    {
        var presentation = For(element);
        var encoder = HtmlEncoder.Default;
        var summary = string.IsNullOrWhiteSpace(element.Summary)
            ? string.Empty
            : $"<p>{encoder.Encode(element.Summary)}</p>";
        var address = string.IsNullOrWhiteSpace(element.Address)
            ? string.Empty
            : $"<p><strong>Dirección:</strong> {encoder.Encode(element.Address)}</p>";
        var link = presentation.DetailPath is null
            ? string.Empty
            : $"<a href=\"{encoder.Encode(presentation.DetailPath)}\">Ver detalles</a>";

        return $"""
            <div class="quake-map-info">
                <small>{encoder.Encode(presentation.Label)}</small>
                <h3>{encoder.Encode(element.Title)}</h3>
                {summary}
                {address}
                {link}
            </div>
            """;
    }
}
