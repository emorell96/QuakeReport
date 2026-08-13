using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Text;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Geospatial;
using QuakeReport.Data.Models;
using StorageGenerics.Core.Contracts;

namespace QuakeReport.ApiService.Shelters;

public sealed record ShelterQueryCriteria(
    Guid EarthquakeId,
    string? SearchText,
    ShelterOperationalStatus? OperationalStatus,
    ShelterModerationStatus? ModerationStatus,
    ShelterSortOption Sort,
    double? Latitude,
    double? Longitude);

public interface IShelterService
{
    IOrderedQueryable<Shelter> GetOrderedQuery(ShelterQueryCriteria criteria);

    IOrderedQueryable<Shelter> GetPendingQuery();

    Task<Shelter?> GetPublicAsync(Guid id, CancellationToken cancellationToken);

    Task<Shelter?> GetByManagementCodeHashAsync(string hash, CancellationToken cancellationToken);

    Task<Shelter?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> PublicExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task CreateAsync(Shelter shelter, CancellationToken cancellationToken);

    Task PersistUpdateAsync(Shelter shelter, CancellationToken cancellationToken);

    Task CreateAbuseReportAsync(ShelterAbuseReport report, CancellationToken cancellationToken);
}

public sealed class ShelterService(
    QuakeReportDbContext db,
    IQueryableRepositoryService<Shelter, Guid> shelters) : IShelterService
{
    public IOrderedQueryable<Shelter> GetOrderedQuery(ShelterQueryCriteria criteria)
    {
        var query = shelters.QueryAll()
            .AsNoTracking()
            .Where(shelter =>
                shelter.EarthquakeId == criteria.EarthquakeId &&
                shelter.ModerationStatus != ShelterModerationStatus.Rejected);

        if (criteria.OperationalStatus is not null)
        {
            query = query.Where(shelter => shelter.OperationalStatus == criteria.OperationalStatus);
        }
        if (criteria.ModerationStatus is not null)
        {
            query = query.Where(shelter => shelter.ModerationStatus == criteria.ModerationStatus);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var normalized = SearchTextNormalizer.Normalize(criteria.SearchText);
            query = query.Where(shelter => shelter.SearchText!.Contains(normalized));
        }

        if (criteria.Latitude is not null && criteria.Longitude is not null)
        {
            return query
                .Where(shelter => shelter.Location != null)
                .OrderByDistanceFrom(
                    GeoPoint.FromCoordinates(criteria.Latitude.Value, criteria.Longitude.Value),
                    db.Database.IsNpgsql())
                .ThenBy(shelter => shelter.Id);
        }

        return criteria.Sort switch
        {
            ShelterSortOption.RecentlyUpdated => query
                .OrderByDescending(shelter => shelter.UpdatedAt)
                .ThenByDescending(shelter => shelter.Id),
            ShelterSortOption.Name => query
                .OrderBy(shelter => shelter.Name)
                .ThenBy(shelter => shelter.Id),
            _ => query
                .OrderByDescending(shelter => shelter.CreatedAt)
                .ThenByDescending(shelter => shelter.Id)
        };
    }

    public IOrderedQueryable<Shelter> GetPendingQuery() =>
        shelters.QueryAll()
            .AsNoTracking()
            .Where(shelter => shelter.ModerationStatus == ShelterModerationStatus.Pending)
            .OrderBy(shelter => shelter.CreatedAt)
            .ThenBy(shelter => shelter.Id);

    public Task<Shelter?> GetPublicAsync(Guid id, CancellationToken cancellationToken) =>
        db.Shelters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                shelter => shelter.Id == id && shelter.ModerationStatus != ShelterModerationStatus.Rejected,
                cancellationToken);

    public Task<Shelter?> GetByManagementCodeHashAsync(string hash, CancellationToken cancellationToken) =>
        db.Shelters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                shelter =>
                    shelter.ManagementCodeHash == hash &&
                    shelter.ModerationStatus != ShelterModerationStatus.Rejected,
                cancellationToken);

    public Task<Shelter?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.Shelters.SingleOrDefaultAsync(shelter => shelter.Id == id, cancellationToken);

    public Task<bool> PublicExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Shelters.AnyAsync(
            shelter => shelter.Id == id && shelter.ModerationStatus != ShelterModerationStatus.Rejected,
            cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Shelters.AnyAsync(shelter => shelter.Id == id, cancellationToken);

    public async Task CreateAsync(Shelter shelter, CancellationToken cancellationToken)
    {
        db.Shelters.Add(shelter);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task PersistUpdateAsync(Shelter shelter, CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);

    public async Task CreateAbuseReportAsync(
        ShelterAbuseReport report,
        CancellationToken cancellationToken)
    {
        db.ShelterAbuseReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
    }
}
