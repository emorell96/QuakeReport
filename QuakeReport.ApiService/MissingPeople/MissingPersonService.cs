using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Text;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;
using StorageGenerics.Core.Contracts;

namespace QuakeReport.ApiService.MissingPeople;

public sealed record MissingPersonQueryCriteria(
    Guid? EarthquakeId,
    string? SearchText,
    MissingPersonStatus Status,
    MissingPersonSortOption Sort);

public interface IMissingPersonService
{
    IOrderedQueryable<MissingPerson> GetOrderedQuery(MissingPersonQueryCriteria criteria);

    IOrderedQueryable<MissingPersonTip> GetPublicTipsQuery(Guid missingPersonId);

    Task<MissingPerson?> GetPublicAsync(Guid id, CancellationToken cancellationToken);

    Task<MissingPerson?> GetByManagementCodeHashAsync(string hash, CancellationToken cancellationToken);

    Task<MissingPerson?> GetByIdentificationAsync(
        Guid earthquakeId,
        IdentificationDocumentType documentType,
        string identificationHash,
        CancellationToken cancellationToken);

    Task<MissingPerson?> GetForUpdateAsync(
        Guid id,
        bool includeLocations,
        CancellationToken cancellationToken);

    Task<MissingPerson?> GetForAuthorizationAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MissingPersonTip>> GetPrivateTipsAsync(
        Guid missingPersonId,
        CancellationToken cancellationToken);

    Task<bool> PublicExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> IdentificationExistsAsync(
        Guid earthquakeId,
        string identificationHash,
        CancellationToken cancellationToken);

    Task CreateAsync(MissingPerson person, CancellationToken cancellationToken);

    Task PersistUpdateAsync(MissingPerson person, CancellationToken cancellationToken);

    Task ReplaceLocationsAsync(
        MissingPerson person,
        IReadOnlyCollection<MissingPersonLocation> locations,
        CancellationToken cancellationToken);

    Task CreateTipAsync(MissingPersonTip tip, CancellationToken cancellationToken);

    Task<bool> HideTipAsync(Guid missingPersonId, Guid tipId, CancellationToken cancellationToken);

    Task CreateAbuseReportAsync(AbuseReport report, CancellationToken cancellationToken);
}

public sealed class MissingPersonService(
    QuakeReportDbContext db,
    IQueryableRepositoryService<MissingPerson, Guid> people,
    IQueryableRepositoryService<MissingPersonTip, Guid> tips) : IMissingPersonService
{
    public IOrderedQueryable<MissingPerson> GetOrderedQuery(MissingPersonQueryCriteria criteria)
    {
        var query = people.QueryAll()
            .AsNoTracking()
            .Include(person => person.Locations)
            .Where(person =>
                person.EarthquakeId == criteria.EarthquakeId &&
                person.Status == criteria.Status);

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var normalized = SearchTextNormalizer.Normalize(criteria.SearchText);
            query = query.Where(person =>
                person.SearchName!.Contains(normalized) ||
                person.Locations.Any(location => location.SearchAddress!.Contains(normalized)));
        }

        return criteria.Sort switch
        {
            MissingPersonSortOption.LastSeenNewest => query
                .OrderByDescending(person => person.LastSeenAt)
                .ThenByDescending(person => person.Id),
            MissingPersonSortOption.Name => query
                .OrderBy(person => person.SearchName)
                .ThenBy(person => person.Id),
            _ => query
                .OrderByDescending(person => person.CreatedAt)
                .ThenByDescending(person => person.Id)
        };
    }

    public IOrderedQueryable<MissingPersonTip> GetPublicTipsQuery(Guid missingPersonId) =>
        tips.QueryAll()
            .AsNoTracking()
            .Where(tip => tip.MissingPersonId == missingPersonId && !tip.IsHidden)
            .OrderByDescending(tip => tip.CreatedAt)
            .ThenByDescending(tip => tip.Id);

    public Task<MissingPerson?> GetPublicAsync(Guid id, CancellationToken cancellationToken) =>
        db.MissingPeople
            .AsNoTracking()
            .Include(person => person.Locations)
            .SingleOrDefaultAsync(
                person => person.Id == id && person.Status != MissingPersonStatus.Closed,
                cancellationToken);

    public Task<MissingPerson?> GetByManagementCodeHashAsync(
        string hash,
        CancellationToken cancellationToken) =>
        db.MissingPeople
            .AsNoTracking()
            .Include(person => person.Locations)
            .SingleOrDefaultAsync(
                person =>
                    person.ManagementCodeHash == hash &&
                    person.Status != MissingPersonStatus.Closed,
                cancellationToken);

    public Task<MissingPerson?> GetByIdentificationAsync(
        Guid earthquakeId,
        IdentificationDocumentType documentType,
        string identificationHash,
        CancellationToken cancellationToken) =>
        db.MissingPeople
            .AsNoTracking()
            .Include(person => person.Locations)
            .SingleOrDefaultAsync(
                person =>
                    person.EarthquakeId == earthquakeId &&
                    person.IdentificationDocumentType == documentType &&
                    person.IdentificationNumberHash == identificationHash &&
                    person.Status != MissingPersonStatus.Closed,
                cancellationToken);

    public Task<MissingPerson?> GetForUpdateAsync(
        Guid id,
        bool includeLocations,
        CancellationToken cancellationToken)
    {
        IQueryable<MissingPerson> query = db.MissingPeople;

        if (includeLocations)
        {
            query = query.Include(person => person.Locations);
        }

        return query.SingleOrDefaultAsync(person => person.Id == id, cancellationToken);
    }

    public Task<MissingPerson?> GetForAuthorizationAsync(Guid id, CancellationToken cancellationToken) =>
        db.MissingPeople
            .AsNoTracking()
            .SingleOrDefaultAsync(person => person.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MissingPersonTip>> GetPrivateTipsAsync(
        Guid missingPersonId,
        CancellationToken cancellationToken) =>
        await db.MissingPersonTips
            .AsNoTracking()
            .Where(tip => tip.MissingPersonId == missingPersonId)
            .OrderByDescending(tip => tip.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> PublicExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.MissingPeople.AnyAsync(
            person => person.Id == id && person.Status != MissingPersonStatus.Closed,
            cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.MissingPeople.AnyAsync(person => person.Id == id, cancellationToken);

    public Task<bool> IdentificationExistsAsync(
        Guid earthquakeId,
        string identificationHash,
        CancellationToken cancellationToken) =>
        db.MissingPeople.AnyAsync(
            person =>
                person.EarthquakeId == earthquakeId &&
                person.IdentificationNumberHash == identificationHash,
            cancellationToken);

    public async Task CreateAsync(MissingPerson person, CancellationToken cancellationToken)
    {
        db.MissingPeople.Add(person);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task PersistUpdateAsync(MissingPerson person, CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);

    public async Task ReplaceLocationsAsync(
        MissingPerson person,
        IReadOnlyCollection<MissingPersonLocation> locations,
        CancellationToken cancellationToken)
    {
        db.MissingPersonLocations.RemoveRange(person.Locations);
        person.Locations = locations.ToList();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateTipAsync(MissingPersonTip tip, CancellationToken cancellationToken)
    {
        db.MissingPersonTips.Add(tip);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> HideTipAsync(
        Guid missingPersonId,
        Guid tipId,
        CancellationToken cancellationToken)
    {
        var tip = await db.MissingPersonTips.SingleOrDefaultAsync(
            item => item.Id == tipId && item.MissingPersonId == missingPersonId,
            cancellationToken);

        if (tip is null)
        {
            return false;
        }

        tip.IsHidden = true;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task CreateAbuseReportAsync(AbuseReport report, CancellationToken cancellationToken)
    {
        db.AbuseReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
    }
}
