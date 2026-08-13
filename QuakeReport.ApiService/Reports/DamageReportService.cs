using Microsoft.EntityFrameworkCore;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;
using StorageGenerics.Core.Contracts;

namespace QuakeReport.ApiService.Reports;

public sealed record DamageReportQueryCriteria(
    Guid EarthquakeId,
    SeverityLevel? Severity,
    ReportSortOption Sort);

public interface IDamageReportService
{
    IOrderedQueryable<DamageReport> GetOrderedQuery(DamageReportQueryCriteria criteria);

    Task<DamageReport?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task CreateAsync(DamageReport report, CancellationToken cancellationToken);

    Task AttachMediaAsync(ReportMedia media, CancellationToken cancellationToken);
}

public sealed class DamageReportService(
    QuakeReportDbContext db,
    IQueryableRepositoryService<DamageReport, Guid> reports) : IDamageReportService
{
    public IOrderedQueryable<DamageReport> GetOrderedQuery(DamageReportQueryCriteria criteria)
    {
        var query = reports.QueryAll()
            .AsNoTracking()
            .Where(report => report.EarthquakeId == criteria.EarthquakeId);

        if (criteria.Severity is not null)
        {
            query = query.Where(report => report.Severity == criteria.Severity.Value);
        }

        return criteria.Sort switch
        {
            ReportSortOption.Oldest => query
                .OrderBy(report => report.CreatedAt)
                .ThenBy(report => report.Id),
            ReportSortOption.HighestSeverity => query
                .OrderByDescending(report => report.Severity)
                .ThenByDescending(report => report.CreatedAt)
                .ThenByDescending(report => report.Id),
            ReportSortOption.LowestSeverity => query
                .OrderBy(report => report.Severity)
                .ThenByDescending(report => report.CreatedAt)
                .ThenByDescending(report => report.Id),
            _ => query
                .OrderByDescending(report => report.CreatedAt)
                .ThenByDescending(report => report.Id)
        };
    }

    public Task<DamageReport?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.DamageReports
            .AsNoTracking()
            .Include(report => report.Media)
            .SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.DamageReports.AnyAsync(report => report.Id == id, cancellationToken);

    public async Task CreateAsync(DamageReport report, CancellationToken cancellationToken)
    {
        db.DamageReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AttachMediaAsync(ReportMedia media, CancellationToken cancellationToken)
    {
        db.ReportMedia.Add(media);
        await db.SaveChangesAsync(cancellationToken);
    }
}
