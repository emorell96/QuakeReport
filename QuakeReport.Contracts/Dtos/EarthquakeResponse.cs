namespace QuakeReport.Contracts.Dtos;

public record EarthquakeResponse(
    Guid Id,
    string Name,
    double Magnitude,
    DateTimeOffset OccurredAt,
    double EpicenterLatitude,
    double EpicenterLongitude);
