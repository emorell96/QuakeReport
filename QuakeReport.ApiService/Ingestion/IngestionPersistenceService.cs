using Microsoft.EntityFrameworkCore;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;

namespace QuakeReport.ApiService.Ingestion;

public interface IIngestionPersistenceService
{
    Task<IngestionSubmission?> FindByIdempotencyKeyAsync(
        IngestionEntityType entityType,
        string idempotencyKeyHash,
        CancellationToken cancellationToken);

    Task<IngestionSubmission?> FindByExternalPostAsync(
        IngestionEntityType entityType,
        IngestionPlatform platform,
        string externalPostId,
        CancellationToken cancellationToken);

    Task CreateSubmissionAsync<TEntity>(
        TEntity entity,
        IngestionSubmission submission,
        CancellationToken cancellationToken)
        where TEntity : class;
}

public sealed class IngestionPersistenceService(QuakeReportDbContext db) : IIngestionPersistenceService
{
    public Task<IngestionSubmission?> FindByIdempotencyKeyAsync(
        IngestionEntityType entityType,
        string idempotencyKeyHash,
        CancellationToken cancellationToken) =>
        db.IngestionSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.EntityType == entityType &&
                    item.IdempotencyKeyHash == idempotencyKeyHash,
                cancellationToken);

    public Task<IngestionSubmission?> FindByExternalPostAsync(
        IngestionEntityType entityType,
        IngestionPlatform platform,
        string externalPostId,
        CancellationToken cancellationToken) =>
        db.IngestionSubmissions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.EntityType == entityType &&
                    item.Platform == platform &&
                    item.ExternalPostId == externalPostId,
                cancellationToken);

    public async Task CreateSubmissionAsync<TEntity>(
        TEntity entity,
        IngestionSubmission submission,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        db.Set<TEntity>().Add(entity);
        db.IngestionSubmissions.Add(submission);
        await db.SaveChangesAsync(cancellationToken);
    }
}
