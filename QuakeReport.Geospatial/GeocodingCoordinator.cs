using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Geospatial;
using QuakeReport.Data.Models;

namespace QuakeReport.Geospatial;

public sealed record GeocodingRunResult(int Examined, int Located, int Queued, int Skipped);

public sealed class GeocodingCoordinator(
    QuakeReportDbContext db,
    IGoogleGeocoder google,
    IConfiguration configuration)
{
    public async Task<GeocodingRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var batchSize = configuration.GetValue("Geocoding:BatchSize", 100);
        var maxConcurrency = Math.Max(1, configuration.GetValue("Geocoding:MaxConcurrency", 5));
        var candidates = await LoadCandidatesAsync(batchSize, cancellationToken);
        var pending = new List<Candidate>();
        var skipped = 0;

        foreach (var candidate in candidates)
        {
            var hash = AddressHash(candidate.Entity.Address!);
            if (await db.GeocodingReviewItems.AnyAsync(item =>
                    item.EntityType == candidate.EntityType && item.EntityId == candidate.Entity.Id &&
                    item.AddressHash == hash &&
                    (item.Status == GeocodingReviewStatus.NeedsReview ||
                     item.Status == GeocodingReviewStatus.Dismissed),
                    cancellationToken))
            {
                skipped++;
            }
            else
            {
                pending.Add(candidate);
            }
        }

        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var outcomes = await Task.WhenAll(pending.Select(async candidate =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try { return (candidate, await google.GeocodeAsync(candidate.Entity.Address!, cancellationToken)); }
            finally { semaphore.Release(); }
        }));

        var located = 0;
        var queued = 0;
        foreach (var (candidate, outcome) in outcomes)
        {
            if (outcome.AutomaticMatch is { } match)
            {
                candidate.Entity.Location = GeoPoint.FromCoordinates(match.Latitude, match.Longitude);
                var hash = AddressHash(candidate.Entity.Address!);
                var existingReview = await db.GeocodingReviewItems.SingleOrDefaultAsync(item =>
                    item.EntityType == candidate.EntityType && item.EntityId == candidate.Entity.Id &&
                    item.AddressHash == hash, cancellationToken);
                if (existingReview is not null)
                {
                    existingReview.Status = GeocodingReviewStatus.Resolved;
                    existingReview.Reason = "Resolved automatically by a later geocoding run.";
                    existingReview.ResolvedAt = DateTimeOffset.UtcNow;
                    existingReview.ResolvedBy = "geocoding-worker";
                    existingReview.AttemptCount++;
                    existingReview.LastAttemptAt = DateTimeOffset.UtcNow;
                }
                located++;
            }
            else
            {
                await UpsertReviewAsync(candidate, outcome, cancellationToken);
                queued++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new(candidates.Count, located, queued, skipped);
    }

    public async Task<bool> RetryAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        var review = await db.GeocodingReviewItems.SingleOrDefaultAsync(item => item.Id == reviewId, cancellationToken);
        if (review is null) return false;
        var entity = await FindEntityAsync(review.EntityType, review.EntityId, cancellationToken);
        if (entity is null || string.IsNullOrWhiteSpace(entity.Address)) return false;
        var outcome = await google.GeocodeAsync(entity.Address, cancellationToken);
        if (outcome.AutomaticMatch is { } match)
        {
            entity.Location = GeoPoint.FromCoordinates(match.Latitude, match.Longitude);
            review.Status = GeocodingReviewStatus.Resolved;
            review.ResolvedAt = DateTimeOffset.UtcNow;
            review.Reason = "Resolved automatically after retry.";
        }
        else
        {
            ApplyOutcome(review, outcome);
        }
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<IGeocodableEntity?> FindEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default) => entityType switch
    {
        nameof(DamageReport) => FindAsync(db.DamageReports, entityId, cancellationToken),
        nameof(MissingPersonLocation) => FindAsync(db.MissingPersonLocations, entityId, cancellationToken),
        nameof(MissingPersonTip) => FindAsync(db.MissingPersonTips, entityId, cancellationToken),
        nameof(CollectionPoint) => FindAsync(db.CollectionPoints, entityId, cancellationToken),
        nameof(Shelter) => FindAsync(db.Shelters, entityId, cancellationToken),
        nameof(HelpRequest) => FindAsync(db.HelpRequests, entityId, cancellationToken),
        nameof(BloodDonationCenter) => FindAsync(db.BloodDonationCenters, entityId, cancellationToken),
        _ => Task.FromResult<IGeocodableEntity?>(null),
    };

    private async Task<List<Candidate>> LoadCandidatesAsync(int limit, CancellationToken cancellationToken)
    {
        var result = new List<Candidate>(limit);
        await AddAsync(db.DamageReports, result, limit, cancellationToken);
        await AddAsync(db.MissingPersonLocations, result, limit, cancellationToken);
        await AddAsync(db.MissingPersonTips, result, limit, cancellationToken);
        await AddAsync(db.CollectionPoints, result, limit, cancellationToken);
        await AddAsync(db.Shelters, result, limit, cancellationToken);
        await AddAsync(db.HelpRequests, result, limit, cancellationToken);
        await AddAsync(db.BloodDonationCenters, result, limit, cancellationToken);
        return result;
    }

    private static async Task AddAsync<TEntity>(DbSet<TEntity> set, List<Candidate> result, int limit, CancellationToken cancellationToken)
        where TEntity : class, IGeocodableEntity
    {
        if (result.Count >= limit) return;
        var entities = await set.Where(entity => entity.Location == null && entity.Address != null && entity.Address != "")
            .Take(limit - result.Count).ToListAsync(cancellationToken);
        result.AddRange(entities.Select(entity => new Candidate(typeof(TEntity).Name, entity)));
    }

    private async Task UpsertReviewAsync(Candidate candidate, GoogleGeocodingOutcome outcome, CancellationToken cancellationToken)
    {
        var hash = AddressHash(candidate.Entity.Address!);
        var review = await db.GeocodingReviewItems.SingleOrDefaultAsync(item =>
            item.EntityType == candidate.EntityType && item.EntityId == candidate.Entity.Id && item.AddressHash == hash,
            cancellationToken);
        if (review is null)
        {
            review = new GeocodingReviewItem
            {
                Id = Guid.NewGuid(), EntityType = candidate.EntityType, EntityId = candidate.Entity.Id,
                AddressSnapshot = candidate.Entity.Address!, AddressHash = hash, Reason = string.Empty,
            };
            db.GeocodingReviewItems.Add(review);
        }
        ApplyOutcome(review, outcome);
    }

    private static void ApplyOutcome(GeocodingReviewItem review, GoogleGeocodingOutcome outcome)
    {
        var candidate = outcome.Candidates.FirstOrDefault();
        review.Status = outcome.Error is null ? GeocodingReviewStatus.NeedsReview : GeocodingReviewStatus.ProviderError;
        review.Reason = outcome.Error ?? (outcome.Candidates.Count == 0 ? "Google returned no results." :
            outcome.Candidates.Count > 1 ? "Google returned multiple results." : "Google returned a low-confidence or partial result.");
        review.CandidateLocation = candidate is null ? null : GeoPoint.FromCoordinates(candidate.Latitude, candidate.Longitude);
        review.FormattedAddress = candidate?.FormattedAddress;
        review.GooglePlaceId = candidate?.PlaceId;
        review.Granularity = candidate?.Granularity;
        review.AttemptCount++;
        review.LastAttemptAt = DateTimeOffset.UtcNow;
    }

    private static async Task<IGeocodableEntity?> FindAsync<TEntity>(DbSet<TEntity> set, Guid id, CancellationToken cancellationToken)
        where TEntity : class, IGeocodableEntity => await set.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    private static string AddressHash(string address) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address.Trim().ToUpperInvariant())));
    private sealed record Candidate(string EntityType, IGeocodableEntity Entity);
}
