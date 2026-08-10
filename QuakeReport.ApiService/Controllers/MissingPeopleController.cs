using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Media;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;

namespace QuakeReport.ApiService.Controllers;

[ApiController]
[Route("api/missing-people")]
public class MissingPeopleController(
    QuakeReportDbContext db,
    ActiveEarthquakeService earthquakes,
    MissingPersonSecurity security,
    ITurnstileValidator turnstile,
    IMissingPersonPhotoStorage photos) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const long MaxPhotoSize = 10 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? query = null,
        [FromQuery] MissingPersonStatus status = MissingPersonStatus.Missing,
        [FromQuery] MissingPersonSortOption sort = MissingPersonSortOption.Newest,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize || !Enum.IsDefined(status) || !Enum.IsDefined(sort))
            return BadRequest("Invalid missing-person query parameters.");

        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null) return Ok(new PagedResponse<MissingPersonSummaryResponse>([], page, pageSize, 0, 0));

        var people = db.MissingPeople.AsNoTracking()
            .Include(person => person.Locations)
            .Where(person => person.EarthquakeId == earthquake.Id && person.Status == status);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = NormalizeSearch(query);
            people = people.Where(person => person.SearchName!.Contains(normalized) ||
                person.Locations.Any(location => location.SearchAddress!.Contains(normalized)));
        }

        var count = await people.CountAsync(cancellationToken);
        var ordered = sort switch
        {
            MissingPersonSortOption.LastSeenNewest => people.OrderByDescending(person => person.LastSeenAt).ThenByDescending(person => person.Id),
            MissingPersonSortOption.Name => people.OrderBy(person => person.SearchName).ThenBy(person => person.Id),
            _ => people.OrderByDescending(person => person.CreatedAt).ThenByDescending(person => person.Id),
        };
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new PagedResponse<MissingPersonSummaryResponse>(
            items.Select(person => person.ToSummaryResponse()).ToList(), page, pageSize, count,
            (int)Math.Ceiling(count / (double)pageSize)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var person = await db.MissingPeople.AsNoTracking()
            .Include(item => item.Locations)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return person is null || person.Status == MissingPersonStatus.Closed
            ? NotFound()
            : Ok(person.ToResponse());
    }

    [HttpGet("{id:guid}/tips")]
    public async Task<IActionResult> Tips(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize) return BadRequest("Invalid pagination.");
        if (!await db.MissingPeople.AnyAsync(person => person.Id == id && person.Status != MissingPersonStatus.Closed, cancellationToken)) return NotFound();
        var tips = db.MissingPersonTips.AsNoTracking().Where(tip => tip.MissingPersonId == id && !tip.IsHidden);
        var count = await tips.CountAsync(cancellationToken);
        var items = await tips.OrderByDescending(tip => tip.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new PagedResponse<MissingPersonTipResponse>(items.Select(tip => tip.ToPublicResponse()).ToList(), page, pageSize, count, (int)Math.Ceiling(count / (double)pageSize)));
    }

    [HttpPost("management/lookup")]
    public async Task<IActionResult> LookupByManagementCode(ManagementCodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ManagementCode)) return BadRequest("Management code is required.");
        var hash = MissingPersonSecurity.HashManagementCode(request.ManagementCode);
        var person = await db.MissingPeople.AsNoTracking().Include(item => item.Locations)
            .SingleOrDefaultAsync(item => item.ManagementCodeHash == hash, cancellationToken);
        return person is null || person.Status == MissingPersonStatus.Closed ? NotFound() : Ok(person.ToResponse());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMissingPersonRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateCreate(request);
        if (validation is not null) return BadRequest(validation);
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable) return StatusCode(503, "Verification service unavailable.");
        if (!challenge.Success) return BadRequest("Human verification failed.");
        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null) return UnprocessableEntity("No active earthquake is configured.");

        var numberHash = string.IsNullOrWhiteSpace(request.IdentificationNumber) ? null : security.HashIdentification(request.IdentificationNumber);
        if (numberHash is not null && await db.MissingPeople.AnyAsync(person => person.EarthquakeId == earthquake.Id && person.IdentificationNumberHash == numberHash, cancellationToken))
            return Conflict("A missing-person listing already exists for this document.");
        var code = MissingPersonSecurity.CreateManagementCode();
        var person = new MissingPerson
        {
            Id = Guid.NewGuid(), EarthquakeId = earthquake.Id, FullName = request.FullName.Trim(), SearchName = NormalizeSearch(request.FullName + " " + request.Aliases),
            Aliases = request.Aliases?.Trim(), ApproximateAge = request.ApproximateAge?.Trim(), IdentificationDocumentType = request.IdentificationDocumentType,
            IdentificationNumberHash = numberHash, IdentificationLastFour = LastFour(request.IdentificationNumber), Description = request.Description.Trim(),
            PhysicalDescription = request.PhysicalDescription?.Trim(), ClothingDescription = request.ClothingDescription?.Trim(), LastSeenAt = request.LastSeenAt,
            ManagementCodeHash = MissingPersonSecurity.HashManagementCode(code), PublicationConsentAt = DateTimeOffset.UtcNow,
        };
        person.Locations = request.Locations.Select(location => new MissingPersonLocation
        {
            Id = Guid.NewGuid(), MissingPersonId = person.Id, Address = location.Address.Trim(), SearchAddress = NormalizeSearch(location.Address),
            Latitude = location.Latitude, Longitude = location.Longitude, Note = location.Note?.Trim(),
        }).ToList();
        db.MissingPeople.Add(person);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = person.Id }, new CreateMissingPersonResponse(person.ToResponse(), code));
    }

    [HttpPost("lookup-by-identification")]
    public async Task<IActionResult> Lookup(IdentificationLookupRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdentificationNumber) || !Enum.IsDefined(request.DocumentType)) return BadRequest("Invalid document.");
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable) return StatusCode(503, "Verification service unavailable.");
        if (!challenge.Success) return BadRequest("Human verification failed.");
        var hash = security.HashIdentification(request.IdentificationNumber);
        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null) return NotFound();
        var person = await db.MissingPeople.AsNoTracking().Include(item => item.Locations)
            .SingleOrDefaultAsync(item => item.EarthquakeId == earthquake.Id && item.IdentificationDocumentType == request.DocumentType && item.IdentificationNumberHash == hash && item.Status != MissingPersonStatus.Closed, cancellationToken);
        return person is null ? NotFound() : Ok(person.ToResponse());
    }

    [HttpPost("{id:guid}/tips")]
    public async Task<IActionResult> CreateTip(Guid id, CreateMissingPersonTipRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000) return BadRequest("A tip message is required.");
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable) return StatusCode(503, "Verification service unavailable.");
        if (!challenge.Success) return BadRequest("Human verification failed.");
        if (!await db.MissingPeople.AnyAsync(person => person.Id == id && person.Status != MissingPersonStatus.Closed, cancellationToken)) return NotFound();
        var tip = new MissingPersonTip { Id = Guid.NewGuid(), MissingPersonId = id, Message = request.Message.Trim(), SightedAt = request.SightedAt, Address = request.Address?.Trim(), Latitude = request.Latitude, Longitude = request.Longitude, ResponderName = request.ResponderName?.Trim(), ResponderPhone = request.ResponderPhone?.Trim(), ResponderEmail = request.ResponderEmail?.Trim() };
        db.MissingPersonTips.Add(tip);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/missing-people/{id}/tips/{tip.Id}", tip.ToPublicResponse());
    }

    [HttpPost("{id:guid}/photo")]
    [RequestSizeLimit(MaxPhotoSize)]
    public async Task<IActionResult> UploadPhoto(Guid id, [FromHeader(Name = "X-Management-Code")] string? code, IFormFile file, CancellationToken cancellationToken)
    {
        var person = await db.MissingPeople.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (person is null) return NotFound();
        if (!Authorize(code, person)) return Unauthorized();
        if (file.Length == 0 || file.Length > MaxPhotoSize || file.ContentType is not ("image/jpeg" or "image/png" or "image/webp")) return BadRequest("Only JPEG, PNG, and WebP images up to 10 MB are allowed.");
        var extension = file.ContentType switch { "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg" };
        await using var stream = file.OpenReadStream();
        person.PhotoUrl = await photos.UploadAsync(id, extension, stream, file.ContentType, cancellationToken);
        person.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { photoUrl = person.PhotoUrl });
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateMissingPersonStatusRequest request, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var person = await db.MissingPeople.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (person is null) return NotFound();
        if (!Authorize(code, person)) return Unauthorized();
        if (!Enum.IsDefined(request.Status)) return BadRequest("Invalid status.");
        person.Status = request.Status; person.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(person.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateMissingPersonRequest request, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Description) || request.Locations is null || request.Locations.Count == 0)
            return BadRequest("Name, description, and at least one address are required.");
        var person = await db.MissingPeople.Include(item => item.Locations).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (person is null) return NotFound();
        if (!Authorize(code, person)) return Unauthorized();
        person.FullName = request.FullName.Trim(); person.SearchName = NormalizeSearch(request.FullName + " " + request.Aliases); person.Aliases = request.Aliases?.Trim(); person.ApproximateAge = request.ApproximateAge?.Trim(); person.Description = request.Description.Trim(); person.PhysicalDescription = request.PhysicalDescription?.Trim(); person.ClothingDescription = request.ClothingDescription?.Trim(); person.LastSeenAt = request.LastSeenAt; person.UpdatedAt = DateTimeOffset.UtcNow;
        db.MissingPersonLocations.RemoveRange(person.Locations);
        person.Locations = request.Locations.Select(location => new MissingPersonLocation { Id = Guid.NewGuid(), MissingPersonId = id, Address = location.Address.Trim(), SearchAddress = NormalizeSearch(location.Address), Latitude = location.Latitude, Longitude = location.Longitude, Note = location.Note?.Trim() }).ToList();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(person.ToResponse());
    }

    [HttpGet("{id:guid}/management/tips")]
    public async Task<IActionResult> PrivateTips(Guid id, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var person = await db.MissingPeople.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (person is null) return NotFound();
        if (!Authorize(code, person)) return Unauthorized();
        var tips = await db.MissingPersonTips.AsNoTracking().Where(tip => tip.MissingPersonId == id).OrderByDescending(tip => tip.CreatedAt).ToListAsync(cancellationToken);
        return Ok(tips.Select(tip => tip.ToPrivateResponse()));
    }

    [HttpPatch("{id:guid}/tips/{tipId:guid}/visibility")]
    public async Task<IActionResult> HideTip(Guid id, Guid tipId, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var person = await db.MissingPeople.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (person is null) return NotFound();
        if (!Authorize(code, person)) return Unauthorized();
        var tip = await db.MissingPersonTips.SingleOrDefaultAsync(item => item.Id == tipId && item.MissingPersonId == id, cancellationToken);
        if (tip is null) return NotFound();
        tip.IsHidden = true;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/abuse-reports")]
    public async Task<IActionResult> Abuse(Guid id, AbuseReportRequest request, CancellationToken cancellationToken)
    {
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable) return StatusCode(503, "Verification service unavailable.");
        if (!challenge.Success) return BadRequest("Human verification failed.");
        if (!await db.MissingPeople.AnyAsync(person => person.Id == id, cancellationToken)) return NotFound();
        db.AbuseReports.Add(new AbuseReport { Id = Guid.NewGuid(), MissingPersonId = id, Reason = request.Reason.Trim(), Details = request.Details?.Trim() });
        await db.SaveChangesAsync(cancellationToken);
        return Accepted();
    }

    private bool Authorize(string? code, MissingPerson person) => !string.IsNullOrWhiteSpace(code) && MissingPersonSecurity.Matches(code, person.ManagementCodeHash);

    private static string NormalizeSearch(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : new(value.Normalize(System.Text.NormalizationForm.FormD).Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character) || char.IsWhiteSpace(character)).Select(char.ToUpperInvariant).ToArray());
    private static string? LastFour(string? value) { var normalized = value is null ? string.Empty : new(value.Where(char.IsLetterOrDigit).ToArray()); return normalized.Length == 0 ? null : normalized[^Math.Min(4, normalized.Length)..]; }
    private static string? ValidateCreate(CreateMissingPersonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length > 200) return "Full name is required.";
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 2000) return "Description is required.";
        if (request.Locations is null || request.Locations.Count == 0 || request.Locations.Any(location => string.IsNullOrWhiteSpace(location.Address))) return "At least one address is required.";
        if (request.IdentificationNumber is not null && request.IdentificationDocumentType is null) return "Document type is required with an identification number.";
        if (!request.PublicationConsent) return "Publication consent is required.";
        if (request.LastSeenAt > DateTimeOffset.UtcNow.AddMinutes(5)) return "Last-seen time cannot be in the future.";
        return null;
    }
}
