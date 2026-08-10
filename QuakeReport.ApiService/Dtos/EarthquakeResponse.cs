using QuakeReport.Data.Models;

namespace QuakeReport.ApiService.Dtos;

public record EarthquakeResponse(
    Guid Id,
    string Name,
    double Magnitude,
    DateTimeOffset OccurredAt,
    double EpicenterLatitude,
    double EpicenterLongitude)
{
    public static EarthquakeResponse FromEntity(Earthquake earthquake) => new(
        earthquake.Id,
        earthquake.Name,
        earthquake.Magnitude,
        earthquake.OccurredAt,
        earthquake.EpicenterLatitude,
        earthquake.EpicenterLongitude);
}
