using Microsoft.EntityFrameworkCore;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;

namespace QuakeReport.ApiService.GeocodingReview;

public interface IGeocodingReviewService
{
    Task<IReadOnlyList<GeocodingReviewItem>> GetLatestAsync(
        GeocodingReviewStatus? status,
        int limit,
        CancellationToken cancellationToken);

    Task<GeocodingReviewItem?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task PersistUpdateAsync(GeocodingReviewItem item, CancellationToken cancellationToken);
}

public sealed class GeocodingReviewService(QuakeReportDbContext db) : IGeocodingReviewService
{
    public async Task<IReadOnlyList<GeocodingReviewItem>> GetLatestAsync(
        GeocodingReviewStatus? status,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = db.GeocodingReviewItems.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(item => item.Status == status);
        }

        return await query
            .OrderByDescending(item => item.LastAttemptAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<GeocodingReviewItem?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        db.GeocodingReviewItems.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task PersistUpdateAsync(GeocodingReviewItem item, CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
