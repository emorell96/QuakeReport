using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Text;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Geospatial;
using QuakeReport.Data.Models;
using StorageGenerics.Core.Contracts;

namespace QuakeReport.ApiService.BloodDonationCenters;

public sealed record BloodDonationCenterQueryCriteria(
    Guid? EarthquakeId,
    string? SearchText,
    BloodDonationCenterType? CenterType,
    BloodDonationOperationalStatus? OperationalStatus,
    BloodDonationModerationStatus? ModerationStatus,
    BloodTypeFlags? BloodTypes,
    BloodComponentFlags? Components,
    BloodDonationSortOption Sort,
    double? Latitude,
    double? Longitude);

public interface IBloodDonationCenterService
{
    IOrderedQueryable<BloodDonationCenter> GetOrderedQuery(BloodDonationCenterQueryCriteria criteria);

    IOrderedQueryable<BloodDonationCenter> GetPendingQuery();

    IOrderedQueryable<BloodDonationCenterComment> GetPublicCommentsQuery(Guid centerId);

    Task<BloodDonationCenter?> GetPublicAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<BloodDonationCenterComment>> GetRecentPublicCommentsAsync(
        Guid centerId,
        int limit,
        CancellationToken cancellationToken);

    Task<BloodDonationCenter?> GetByManagementCodeHashAsync(string hash, CancellationToken cancellationToken);

    Task<BloodDonationCenter?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> PublicExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task CreateAsync(BloodDonationCenter center, CancellationToken cancellationToken);

    Task PersistUpdateAsync(BloodDonationCenter center, CancellationToken cancellationToken);

    Task CreateCommentAsync(BloodDonationCenterComment comment, CancellationToken cancellationToken);

    Task<bool> HideCommentAsync(Guid centerId, Guid commentId, CancellationToken cancellationToken);

    Task CreateAbuseReportAsync(BloodDonationCenterAbuseReport report, CancellationToken cancellationToken);
}

public sealed class BloodDonationCenterService(
    QuakeReportDbContext db,
    IQueryableRepositoryService<BloodDonationCenter, Guid> centers,
    IQueryableRepositoryService<BloodDonationCenterComment, Guid> comments) : IBloodDonationCenterService
{
    public IOrderedQueryable<BloodDonationCenter> GetOrderedQuery(
        BloodDonationCenterQueryCriteria criteria)
    {
        var query = centers.QueryAll()
            .AsNoTracking()
            .Where(center =>
                center.EarthquakeId == criteria.EarthquakeId &&
                center.ModerationStatus != BloodDonationModerationStatus.Rejected);

        if (criteria.ModerationStatus is not null)
        {
            query = query.Where(center => center.ModerationStatus == criteria.ModerationStatus);
        }

        if (criteria.CenterType is not null)
        {
            query = query.Where(center => center.CenterType == criteria.CenterType);
        }

        if (criteria.OperationalStatus is not null)
        {
            query = query.Where(center => center.OperationalStatus == criteria.OperationalStatus);
        }
        else
        {
            var now = DateTimeOffset.UtcNow;
            query = query.Where(center =>
                center.OperationalStatus != BloodDonationOperationalStatus.Closed &&
                (center.EndsAt == null || center.EndsAt > now));
        }

        if (criteria.BloodTypes is not null)
        {
            query = query.Where(center => (center.BloodTypes & criteria.BloodTypes.Value) != 0);
        }

        if (criteria.Components is not null)
        {
            query = query.Where(center => (center.Components & criteria.Components.Value) != 0);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var normalized = SearchTextNormalizer.Normalize(criteria.SearchText);
            query = query.Where(center => center.SearchText!.Contains(normalized));
        }

        if (criteria.Latitude is not null && criteria.Longitude is not null)
        {
            return query
                .Where(center => center.Location != null)
                .OrderByDistanceFrom(
                    GeoPoint.FromCoordinates(criteria.Latitude.Value, criteria.Longitude.Value),
                    db.Database.IsNpgsql())
                .ThenBy(center => center.Id);
        }

        return criteria.Sort switch
        {
            BloodDonationSortOption.RecentlyUpdated => query
                .OrderByDescending(center => center.UpdatedAt)
                .ThenByDescending(center => center.Id),
            BloodDonationSortOption.Name => query
                .OrderBy(center => center.Name)
                .ThenBy(center => center.Id),
            _ => query
                .OrderByDescending(center => center.CreatedAt)
                .ThenByDescending(center => center.Id)
        };
    }

    public IOrderedQueryable<BloodDonationCenter> GetPendingQuery() =>
        centers.QueryAll()
            .AsNoTracking()
            .Where(center => center.ModerationStatus == BloodDonationModerationStatus.Pending)
            .OrderBy(center => center.CreatedAt)
            .ThenBy(center => center.Id);

    public IOrderedQueryable<BloodDonationCenterComment> GetPublicCommentsQuery(Guid centerId) =>
        comments.QueryAll()
            .AsNoTracking()
            .Where(comment => comment.BloodDonationCenterId == centerId && !comment.IsHidden)
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id);

    public Task<BloodDonationCenter?> GetPublicAsync(Guid id, CancellationToken cancellationToken) =>
        db.BloodDonationCenters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                center => center.Id == id && center.ModerationStatus != BloodDonationModerationStatus.Rejected,
                cancellationToken);

    public async Task<IReadOnlyList<BloodDonationCenterComment>> GetRecentPublicCommentsAsync(
        Guid centerId,
        int limit,
        CancellationToken cancellationToken) =>
        await db.BloodDonationCenterComments
            .AsNoTracking()
            .Where(comment => comment.BloodDonationCenterId == centerId && !comment.IsHidden)
            .OrderByDescending(comment => comment.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<BloodDonationCenter?> GetByManagementCodeHashAsync(
        string hash,
        CancellationToken cancellationToken) =>
        db.BloodDonationCenters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                center =>
                    center.ManagementCodeHash == hash &&
                    center.ModerationStatus != BloodDonationModerationStatus.Rejected,
                cancellationToken);

    public Task<BloodDonationCenter?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.BloodDonationCenters.SingleOrDefaultAsync(center => center.Id == id, cancellationToken);

    public Task<bool> PublicExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.BloodDonationCenters.AnyAsync(
            center => center.Id == id && center.ModerationStatus != BloodDonationModerationStatus.Rejected,
            cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.BloodDonationCenters.AnyAsync(center => center.Id == id, cancellationToken);

    public async Task CreateAsync(BloodDonationCenter center, CancellationToken cancellationToken)
    {
        db.BloodDonationCenters.Add(center);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task PersistUpdateAsync(BloodDonationCenter center, CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);

    public async Task CreateCommentAsync(
        BloodDonationCenterComment comment,
        CancellationToken cancellationToken)
    {
        db.BloodDonationCenterComments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HideCommentAsync(
        Guid centerId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var comment = await db.BloodDonationCenterComments.SingleOrDefaultAsync(
            item => item.Id == commentId && item.BloodDonationCenterId == centerId,
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
        BloodDonationCenterAbuseReport report,
        CancellationToken cancellationToken)
    {
        db.BloodDonationCenterAbuseReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
    }
}
