using QuakeReport.Contracts.Enums;

namespace QuakeReport.Contracts.Dtos;

public sealed record MapElementResponse(
    Guid MarkerId,
    Guid EntityId,
    MapElementType Type,
    string Title,
    string? Summary,
    string? Address,
    double Latitude,
    double Longitude);

public sealed record MapOverviewResponse(
    EarthquakeResponse Earthquake,
    IReadOnlyList<MapElementResponse> Elements);
