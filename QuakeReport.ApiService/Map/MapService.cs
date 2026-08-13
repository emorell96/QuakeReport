using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;

namespace QuakeReport.ApiService.Map;

public sealed record MapElement(
    Guid MarkerId,
    Guid EntityId,
    MapElementType Type,
    string Title,
    string? Summary,
    string? Address,
    double Latitude,
    double Longitude);

public sealed record MapOverview(
    Earthquake Earthquake,
    IReadOnlyList<MapElement> Elements);

public interface IMapService
{
    Task<MapOverview> GetOverviewAsync(
        Earthquake earthquake,
        CancellationToken cancellationToken);
}

public static class LatitudeLongitudeExtensions
{
    public static double Latitude(this Point point) => point.Y;
    public static double Longitude(this Point point) => point.X;
}

public sealed class MapService(QuakeReportDbContext db) : IMapService
{

    public async Task<MapOverview> GetOverviewAsync(
        Earthquake earthquake,
        CancellationToken cancellationToken)
    {
        var elements = new List<MapElement>
        {
            new(
                earthquake.Id,
                earthquake.Id,
                MapElementType.Earthquake,
                earthquake.Name,
                $"Magnitud {earthquake.Magnitude:0.0}",
                null,
                earthquake.Location.Y,
                earthquake.Location.X),
        };

        elements.AddRange(await LoadDamageReportsAsync(earthquake.Id, cancellationToken));
        elements.AddRange(await LoadSheltersAsync(earthquake.Id, cancellationToken));
        elements.AddRange(await LoadCollectionPointsAsync(earthquake.Id, cancellationToken));
        elements.AddRange(await LoadBloodDonationCentersAsync(earthquake.Id, cancellationToken));
        elements.AddRange(await LoadHelpRequestsAsync(earthquake.Id, cancellationToken));
        elements.AddRange(await LoadMissingPeopleAsync(earthquake.Id, cancellationToken));

        return new MapOverview(earthquake, elements);
    }

    private async Task<IReadOnlyList<MapElement>> LoadDamageReportsAsync(
        Guid earthquakeId,
        CancellationToken cancellationToken) =>
        await db.DamageReports
            .AsNoTracking()
            .Where(report => report.EarthquakeId == earthquakeId)
            .OrderByDescending(report => report.CreatedAt)
            .ThenByDescending(report => report.Id)
            
            .Select(report => new MapElement(
                report.Id,
                report.Id,
                MapElementType.DamageReport,
                $"Reporte de daño · {report.Severity}",
                report.Description,
                report.Address,
                report.Location.Latitude(),
                report.Location.Longitude())).ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<MapElement>> LoadSheltersAsync(
        Guid earthquakeId,
        CancellationToken cancellationToken) =>
        (await db.Shelters
            .AsNoTracking()
            .Where(shelter =>
                shelter.EarthquakeId == earthquakeId &&
                shelter.Location != null &&
                shelter.ModerationStatus != ShelterModerationStatus.Rejected)
            .OrderBy(shelter => shelter.Name)
            .ThenBy(shelter => shelter.Id)
            .ToListAsync(cancellationToken))
            .Select(shelter => new MapElement(
                shelter.Id,
                shelter.Id,
                MapElementType.Shelter,
                shelter.Name,
                shelter.Description,
                shelter.Address,
                shelter.Location!.Latitude(),
                shelter.Location!.Longitude())).ToList();

    private async Task<IReadOnlyList<MapElement>> LoadCollectionPointsAsync(
        Guid earthquakeId,
        CancellationToken cancellationToken) =>
        (await db.CollectionPoints
            .AsNoTracking()
            .Where(point =>
                point.EarthquakeId == earthquakeId &&
                point.Location != null &&
                point.ModerationStatus != CollectionPointModerationStatus.Rejected)
            .OrderBy(point => point.Name)
            .ThenBy(point => point.Id)
            
            .ToListAsync(cancellationToken)).Select(point => new MapElement(
                point.Id,
                point.Id,
                MapElementType.CollectionPoint,
                point.Name,
                point.NeedsSummary,
                point.Address,
                point.Location!.Latitude(),
                point.Location!.Longitude())).ToList();

    private async Task<IReadOnlyList<MapElement>> LoadBloodDonationCentersAsync(
        Guid earthquakeId,
        CancellationToken cancellationToken) =>
        await db.BloodDonationCenters
            .AsNoTracking()
            .Where(center =>
                center.EarthquakeId == earthquakeId &&
                center.Location != null &&
                center.ModerationStatus != BloodDonationModerationStatus.Rejected)
            .OrderBy(center => center.Name)
            .ThenBy(center => center.Id)
            .Select(center => new MapElement(
                center.Id,
                center.Id,
                MapElementType.BloodDonationCenter,
                center.Name,
                center.NeedsSummary,
                center.Address,
                center.Location!.Latitude(),
                center.Location!.Longitude()))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<MapElement>> LoadHelpRequestsAsync(
        Guid earthquakeId,
        CancellationToken cancellationToken) =>
        await db.HelpRequests
            .AsNoTracking()
            .Where(request =>
                request.EarthquakeId == earthquakeId &&
                request.Location != null &&
                request.ModerationStatus != HelpRequestModerationStatus.Rejected)
            .OrderByDescending(request => request.Priority)
            .ThenByDescending(request => request.CreatedAt)
            .ThenByDescending(request => request.Id)
            .Select(request => new MapElement(
                request.Id,
                request.Id,
                MapElementType.HelpRequest,
                request.Title,
                request.NeedDetails,
                request.Address,
                request.Location!.Latitude(),
                request.Location!.Longitude()))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<MapElement>> LoadMissingPeopleAsync(
        Guid earthquakeId,
        CancellationToken cancellationToken) =>
        await db.MissingPersonLocations
            .AsNoTracking()
            .Where(location =>
                location.MissingPerson != null &&
                location.MissingPerson.EarthquakeId == earthquakeId &&
                location.MissingPerson.Status != MissingPersonStatus.Closed &&
                location.Location != null)
            .OrderBy(location => location.MissingPerson!.FullName)
            .ThenBy(location => location.Id)
            .Select(location => new MapElement(
                location.Id,
                location.MissingPersonId,
                MapElementType.MissingPerson,
                location.MissingPerson!.FullName,
                location.Note ?? location.MissingPerson.Description,
                location.Address,
                location.Location!.Latitude(),
                location.Location!.Longitude()))
            .ToListAsync(cancellationToken);
}
