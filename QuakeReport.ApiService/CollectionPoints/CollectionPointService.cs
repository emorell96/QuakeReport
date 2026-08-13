using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Text;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Geospatial;
using QuakeReport.Data.Models;
using StorageGenerics.Core.Contracts;

namespace QuakeReport.ApiService.CollectionPoints;

public sealed record CollectionPointQueryCriteria(
    Guid EarthquakeId,
    string? SearchText,
    CollectionPointOperationalStatus? OperationalStatus,
    CollectionPointModerationStatus? ModerationStatus,
    CollectionPointSortOption Sort,
    double? Latitude,
    double? Longitude);

public interface ICollectionPointService
{
    IOrderedQueryable<CollectionPoint> GetOrderedQuery(CollectionPointQueryCriteria criteria);

    IOrderedQueryable<CollectionPoint> GetPendingQuery();

    IOrderedQueryable<CollectionPointComment> GetPublicCommentsQuery(Guid collectionPointId);

    Task<CollectionPoint?> GetPublicAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CollectionPointComment>> GetRecentPublicCommentsAsync(
        Guid collectionPointId,
        int limit,
        CancellationToken cancellationToken);

    Task<CollectionPoint?> GetByManagementCodeHashAsync(
        string managementCodeHash,
        CancellationToken cancellationToken);

    Task<CollectionPoint?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> PublicExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task CreateAsync(CollectionPoint collectionPoint, CancellationToken cancellationToken);

    Task PersistUpdateAsync(CollectionPoint collectionPoint, CancellationToken cancellationToken);

    Task CreateCommentAsync(CollectionPointComment comment, CancellationToken cancellationToken);

    Task<bool> HideCommentAsync(Guid collectionPointId, Guid commentId, CancellationToken cancellationToken);

    Task CreateAbuseReportAsync(CollectionPointAbuseReport abuseReport, CancellationToken cancellationToken);
}

public sealed class CollectionPointService(
    QuakeReportDbContext db,
    IQueryableRepositoryService<CollectionPoint, Guid> collectionPoints,
    IQueryableRepositoryService<CollectionPointComment, Guid> comments) : ICollectionPointService
{
    public IOrderedQueryable<CollectionPoint> GetOrderedQuery(CollectionPointQueryCriteria criteria)
    {
        var query = collectionPoints.QueryAll()
            .AsNoTracking()
            .Where(point =>
                point.EarthquakeId == criteria.EarthquakeId &&
                point.ModerationStatus != CollectionPointModerationStatus.Rejected);

        if (criteria.ModerationStatus is not null)
        {
            query = query.Where(point => point.ModerationStatus == criteria.ModerationStatus);
        }

        if (criteria.OperationalStatus is not null)
        {
            query = query.Where(point => point.OperationalStatus == criteria.OperationalStatus);
        }
        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var normalized = SearchTextNormalizer.Normalize(criteria.SearchText);
            query = query.Where(point => point.SearchText!.Contains(normalized));
        }

        if (criteria.Latitude is not null && criteria.Longitude is not null)
        {
            return query
                .Where(point => point.Location != null)
                .OrderByDistanceFrom(
                    GeoPoint.FromCoordinates(criteria.Latitude.Value, criteria.Longitude.Value),
                    db.Database.IsNpgsql())
                .ThenBy(point => point.Id);
        }

        return criteria.Sort switch
        {
            CollectionPointSortOption.RecentlyUpdated => query
                .OrderByDescending(point => point.UpdatedAt)
                .ThenByDescending(point => point.Id),
            CollectionPointSortOption.Name => query
                .OrderBy(point => point.Name)
                .ThenBy(point => point.Id),
            _ => query
                .OrderByDescending(point => point.CreatedAt)
                .ThenByDescending(point => point.Id)
        };
    }

    public IOrderedQueryable<CollectionPoint> GetPendingQuery() =>
        collectionPoints.QueryAll()
            .AsNoTracking()
            .Where(point => point.ModerationStatus == CollectionPointModerationStatus.Pending)
            .OrderBy(point => point.CreatedAt)
            .ThenBy(point => point.Id);

    public IOrderedQueryable<CollectionPointComment> GetPublicCommentsQuery(Guid collectionPointId) =>
        comments.QueryAll()
            .AsNoTracking()
            .Where(comment => comment.CollectionPointId == collectionPointId && !comment.IsHidden)
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id);

    public Task<CollectionPoint?> GetPublicAsync(Guid id, CancellationToken cancellationToken) =>
        db.CollectionPoints
            .AsNoTracking()
            .SingleOrDefaultAsync(
                point => point.Id == id && point.ModerationStatus != CollectionPointModerationStatus.Rejected,
                cancellationToken);

    public async Task<IReadOnlyList<CollectionPointComment>> GetRecentPublicCommentsAsync(
        Guid collectionPointId,
        int limit,
        CancellationToken cancellationToken) =>
        await db.CollectionPointComments
            .AsNoTracking()
            .Where(comment => comment.CollectionPointId == collectionPointId && !comment.IsHidden)
            .OrderByDescending(comment => comment.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<CollectionPoint?> GetByManagementCodeHashAsync(
        string managementCodeHash,
        CancellationToken cancellationToken) =>
        db.CollectionPoints
            .AsNoTracking()
            .SingleOrDefaultAsync(
                point =>
                    point.ManagementCodeHash == managementCodeHash &&
                    point.ModerationStatus != CollectionPointModerationStatus.Rejected,
                cancellationToken);

    public Task<CollectionPoint?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.CollectionPoints.SingleOrDefaultAsync(point => point.Id == id, cancellationToken);

    public Task<bool> PublicExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.CollectionPoints.AnyAsync(
            point => point.Id == id && point.ModerationStatus != CollectionPointModerationStatus.Rejected,
            cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.CollectionPoints.AnyAsync(point => point.Id == id, cancellationToken);

    public async Task CreateAsync(CollectionPoint collectionPoint, CancellationToken cancellationToken)
    {
        db.CollectionPoints.Add(collectionPoint);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task PersistUpdateAsync(CollectionPoint collectionPoint, CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);

    public async Task CreateCommentAsync(
        CollectionPointComment comment,
        CancellationToken cancellationToken)
    {
        db.CollectionPointComments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HideCommentAsync(
        Guid collectionPointId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var comment = await db.CollectionPointComments.SingleOrDefaultAsync(
            item => item.Id == commentId && item.CollectionPointId == collectionPointId,
            cancellationToken);

        if (comment is null)
        {
            return false;
        }

        comment.IsHidden = true;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task CreateAbuseReportAsync(
        CollectionPointAbuseReport abuseReport,
        CancellationToken cancellationToken)
    {
        db.CollectionPointAbuseReports.Add(abuseReport);
        await db.SaveChangesAsync(cancellationToken);
    }
}
