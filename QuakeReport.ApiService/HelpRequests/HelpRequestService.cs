using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Text;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;
using StorageGenerics.Core.Contracts;

namespace QuakeReport.ApiService.HelpRequests;

public sealed record HelpRequestQueryCriteria(
    Guid EarthquakeId,
    string? SearchText,
    HelpRequestPriority? Priority,
    HelpNeedCategory? Category,
    HelpRequestStatus? Status,
    HelpRequestModerationStatus? ModerationStatus,
    HelpRequestSortOption Sort);

public interface IHelpRequestService
{
    IOrderedQueryable<HelpRequest> GetOrderedQuery(HelpRequestQueryCriteria criteria);

    IOrderedQueryable<HelpRequest> GetPendingQuery();

    IOrderedQueryable<HelpRequestComment> GetPublicCommentsQuery(Guid helpRequestId);

    Task<HelpRequest?> GetPublicAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<HelpRequestComment>> GetRecentPublicCommentsAsync(
        Guid helpRequestId,
        int limit,
        CancellationToken cancellationToken);

    Task<HelpRequest?> GetByManagementCodeHashAsync(string hash, CancellationToken cancellationToken);

    Task<HelpRequest?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> PublicExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task CreateAsync(HelpRequest helpRequest, CancellationToken cancellationToken);

    Task PersistUpdateAsync(HelpRequest helpRequest, CancellationToken cancellationToken);

    Task CreateCommentAsync(HelpRequestComment comment, CancellationToken cancellationToken);

    Task<bool> HideCommentAsync(Guid helpRequestId, Guid commentId, CancellationToken cancellationToken);

    Task CreateAbuseReportAsync(HelpRequestAbuseReport report, CancellationToken cancellationToken);
}

public sealed class HelpRequestService(
    QuakeReportDbContext db,
    IQueryableRepositoryService<HelpRequest, Guid> helpRequests,
    IQueryableRepositoryService<HelpRequestComment, Guid> comments) : IHelpRequestService
{
    public IOrderedQueryable<HelpRequest> GetOrderedQuery(HelpRequestQueryCriteria criteria)
    {
        var query = helpRequests.QueryAll()
            .AsNoTracking()
            .Where(request =>
                request.EarthquakeId == criteria.EarthquakeId &&
                request.ModerationStatus != HelpRequestModerationStatus.Rejected);

        if (criteria.Status is not null)
        {
            query = query.Where(request => request.Status == criteria.Status);
        }

        if (criteria.Priority is not null)
        {
            query = query.Where(request => request.Priority == criteria.Priority);
        }

        if (criteria.Category is not null)
        {
            query = query.Where(request =>
                ((int)request.NeedCategories & (int)criteria.Category.Value) != 0);
        }

        if (criteria.ModerationStatus is not null)
        {
            query = query.Where(request => request.ModerationStatus == criteria.ModerationStatus);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var normalized = SearchTextNormalizer.Normalize(criteria.SearchText);
            query = query.Where(request => request.SearchText!.Contains(normalized));
        }

        return criteria.Sort switch
        {
            HelpRequestSortOption.Newest => query
                .OrderByDescending(request => request.CreatedAt)
                .ThenByDescending(request => request.Id),
            HelpRequestSortOption.RecentlyUpdated => query
                .OrderByDescending(request => request.UpdatedAt)
                .ThenByDescending(request => request.Id),
            HelpRequestSortOption.NeededSoon => query
                .OrderBy(request => request.NeededBy == null)
                .ThenBy(request => request.NeededBy)
                .ThenByDescending(request => request.Priority)
                .ThenBy(request => request.Id),
            _ => query
                .OrderByDescending(request => request.Priority)
                .ThenByDescending(request => request.CreatedAt)
                .ThenByDescending(request => request.Id)
        };
    }

    public IOrderedQueryable<HelpRequest> GetPendingQuery() =>
        helpRequests.QueryAll()
            .AsNoTracking()
            .Where(request => request.ModerationStatus == HelpRequestModerationStatus.Pending)
            .OrderBy(request => request.CreatedAt)
            .ThenBy(request => request.Id);

    public IOrderedQueryable<HelpRequestComment> GetPublicCommentsQuery(Guid helpRequestId) =>
        comments.QueryAll()
            .AsNoTracking()
            .Where(comment => comment.HelpRequestId == helpRequestId && !comment.IsHidden)
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id);

    public Task<HelpRequest?> GetPublicAsync(Guid id, CancellationToken cancellationToken) =>
        db.HelpRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                request => request.Id == id && request.ModerationStatus != HelpRequestModerationStatus.Rejected,
                cancellationToken);

    public async Task<IReadOnlyList<HelpRequestComment>> GetRecentPublicCommentsAsync(
        Guid helpRequestId,
        int limit,
        CancellationToken cancellationToken) =>
        await db.HelpRequestComments
            .AsNoTracking()
            .Where(comment => comment.HelpRequestId == helpRequestId && !comment.IsHidden)
            .OrderByDescending(comment => comment.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<HelpRequest?> GetByManagementCodeHashAsync(string hash, CancellationToken cancellationToken) =>
        db.HelpRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                request =>
                    request.ManagementCodeHash == hash &&
                    request.ModerationStatus != HelpRequestModerationStatus.Rejected,
                cancellationToken);

    public Task<HelpRequest?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.HelpRequests.SingleOrDefaultAsync(request => request.Id == id, cancellationToken);

    public Task<bool> PublicExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.HelpRequests.AnyAsync(
            request => request.Id == id && request.ModerationStatus != HelpRequestModerationStatus.Rejected,
            cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.HelpRequests.AnyAsync(request => request.Id == id, cancellationToken);

    public async Task CreateAsync(HelpRequest helpRequest, CancellationToken cancellationToken)
    {
        db.HelpRequests.Add(helpRequest);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task PersistUpdateAsync(HelpRequest helpRequest, CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);

    public async Task CreateCommentAsync(HelpRequestComment comment, CancellationToken cancellationToken)
    {
        db.HelpRequestComments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HideCommentAsync(
        Guid helpRequestId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var comment = await db.HelpRequestComments.SingleOrDefaultAsync(
            item => item.Id == commentId && item.HelpRequestId == helpRequestId,
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
        HelpRequestAbuseReport report,
        CancellationToken cancellationToken)
    {
        db.HelpRequestAbuseReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
    }
}
