using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Media;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.ApiService.Pagination;
using QuakeReport.ApiService.Text;
using QuakeReport.ApiService.Validation;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Core.Models.API;
using QuakeReport.Data.Models;
using QuakeReport.Data.Geospatial;
using StorageGenerics.Core.Models;
using StorageGenerics.Extensions;

namespace QuakeReport.ApiService.Controllers;

[ApiController]
[Route("api/missing-people")]
public class MissingPeopleController(
    IMissingPersonService missingPeople,
    IActiveEarthquakeService earthquakes,
    MissingPersonSecurity security,
    ITurnstileValidator turnstile,
    IMissingPersonPhotoStorage photos,
    IValidator<PaginationRequest> paginationValidator,
    IValidator<PagedRequest<MissingPersonSearchFilter>> searchValidator) : ControllerBase
{
    private const long MaxPhotoSize = 10 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<PagedResult<MissingPersonSummaryResponse>>> List(
        [FromQuery] PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var validation = await paginationValidator.ValidateAsync(pagination, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToProblemDetails("Invalid pagination parameters."));
        }

        var earthquakeId = await earthquakes.ResolveEarthquakeIdAsync(null, cancellationToken);
        if (earthquakeId is null)
        {
            return UnprocessableEntity("No active earthquake is configured.");
        }

        var criteria = new MissingPersonQueryCriteria(
            earthquakeId.Value,
            null,
            null,
            MissingPersonSortOption.Newest);
        var ordered = missingPeople.GetOrderedQuery(criteria);
        var projected = ordered.SelectOrdered(person => person.ToSummaryResponse());
        var result = await projected.ToPagedResultAsync(
            pagination.Page,
            pagination.PageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<ActionResult<PagedResult<MissingPersonSummaryResponse>>> Search(
        [FromBody] PagedRequest<MissingPersonSearchFilter> request,
        CancellationToken cancellationToken = default)
    {
        var validation = await searchValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToProblemDetails("Invalid missing-person search."));
        }

        var filter = request.Filter!;
        var earthquakeId = await earthquakes.ResolveEarthquakeIdAsync(
            filter.EarthquakeId,
            cancellationToken);
        if (earthquakeId is null)
        {
            return UnprocessableEntity("No active earthquake is configured.");
        }

        var criteria = new MissingPersonQueryCriteria(
            earthquakeId.Value,
            filter.SearchText,
            filter.Status,
            filter.Sort);
        var ordered = missingPeople.GetOrderedQuery(criteria);
        var projected = ordered.SelectOrdered(person => person.ToSummaryResponse());
        var result = await projected.ToPagedResultAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MissingPersonResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var person = await missingPeople.GetPublicAsync(id, cancellationToken);
        return person is null ? NotFound() : Ok(person.ToResponse());
    }

    [HttpGet("{id:guid}/tips")]
    public async Task<ActionResult<PagedResult<MissingPersonTipResponse>>> Tips(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationParameters.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        if (!PaginationParameters.IsValid(page, pageSize))
        {
            return BadRequest("Invalid pagination.");
        }
        if (!await missingPeople.PublicExistsAsync(id, cancellationToken))
        {
            return NotFound();
        }
        var ordered = missingPeople.GetPublicTipsQuery(id);
        var projected = ordered.SelectOrdered(tip => tip.ToPublicResponse());
        return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
    }

    [HttpPost("management/lookup")]
    public async Task<ActionResult<MissingPersonResponse>> LookupByManagementCode(ManagementCodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ManagementCode))
        {
            return BadRequest("Management code is required.");
        }
        var hash = MissingPersonSecurity.HashManagementCode(request.ManagementCode);
        var person = await missingPeople.GetByManagementCodeHashAsync(hash, cancellationToken);
        return person is null ? NotFound() : Ok(person.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<CreateMissingPersonResponse>> Create(CreateMissingPersonRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateCreate(request);
        if (validation is not null)
        {
            return BadRequest(validation);
        }
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable)
        {
            return StatusCode(503, "Verification service unavailable.");
        }
        if (!challenge.Success)
        {
            return BadRequest("Human verification failed.");
        }
        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null)
        {
            return UnprocessableEntity("No active earthquake is configured.");
        }

        var numberHash = string.IsNullOrWhiteSpace(request.IdentificationNumber) ? null : security.HashIdentification(request.IdentificationNumber);
        if (numberHash is not null &&
            await missingPeople.IdentificationExistsAsync(earthquake.Id, numberHash, cancellationToken))
        {
            return Conflict("A missing-person listing already exists for this document.");
        }
        var code = MissingPersonSecurity.CreateManagementCode();
        var person = new MissingPerson
        {
            Id = Guid.NewGuid(),
            EarthquakeId = earthquake.Id,
            FullName = request.FullName.Trim(),
            SearchName = SearchTextNormalizer.Normalize(request.FullName + " " + request.Aliases),
            Aliases = request.Aliases?.Trim(),
            ApproximateAge = request.ApproximateAge?.Trim(),
            IdentificationDocumentType = request.IdentificationDocumentType,
            IdentificationNumberHash = numberHash,
            IdentificationLastFour = LastFour(request.IdentificationNumber),
            Description = request.Description.Trim(),
            PhysicalDescription = request.PhysicalDescription?.Trim(),
            ClothingDescription = request.ClothingDescription?.Trim(),
            LastSeenAt = request.LastSeenAt,
            ManagementCodeHash = MissingPersonSecurity.HashManagementCode(code),
            PublicationConsentAt = DateTimeOffset.UtcNow,
        };
        person.Locations = request.Locations.Select(location => new MissingPersonLocation
        {
            Id = Guid.NewGuid(),
            MissingPersonId = person.Id,
            Address = location.Address.Trim(),
            SearchAddress = SearchTextNormalizer.Normalize(location.Address),
            Location = GeoPoint.FromCoordinates(location.Latitude, location.Longitude),
            Note = location.Note?.Trim(),
        }).ToList();
        await missingPeople.CreateAsync(person, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = person.Id }, new CreateMissingPersonResponse(person.ToResponse(), code));
    }

    [HttpPost("lookup-by-identification")]
    public async Task<ActionResult<MissingPersonResponse>> Lookup(IdentificationLookupRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdentificationNumber) || !Enum.IsDefined(request.DocumentType))
        {
            return BadRequest("Invalid document.");
        }
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable)
        {
            return StatusCode(503, "Verification service unavailable.");
        }
        if (!challenge.Success)
        {
            return BadRequest("Human verification failed.");
        }
        var hash = security.HashIdentification(request.IdentificationNumber);
        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null)
        {
            return NotFound();
        }
        var person = await missingPeople.GetByIdentificationAsync(
            earthquake.Id,
            request.DocumentType,
            hash,
            cancellationToken);
        return person is null ? NotFound() : Ok(person.ToResponse());
    }

    [HttpPost("{id:guid}/tips")]
    public async Task<ActionResult<MissingPersonTipResponse>> CreateTip(Guid id, CreateMissingPersonTipRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000)
        {
            return BadRequest("A tip message is required.");
        }
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable)
        {
            return StatusCode(503, "Verification service unavailable.");
        }
        if (!challenge.Success)
        {
            return BadRequest("Human verification failed.");
        }
        if (!await missingPeople.PublicExistsAsync(id, cancellationToken))
        {
            return NotFound();
        }
        var tip = new MissingPersonTip
        {
            Id = Guid.NewGuid(),
            MissingPersonId = id,
            Message = request.Message.Trim(),
            SightedAt = request.SightedAt,
            Address = request.Address?.Trim(),
            Location = GeoPoint.FromCoordinates(request.Latitude, request.Longitude),
            ResponderName = request.ResponderName?.Trim(),
            ResponderPhone = request.ResponderPhone?.Trim(),
            ResponderEmail = request.ResponderEmail?.Trim()
        };
        await missingPeople.CreateTipAsync(tip, cancellationToken);
        return Created($"/api/missing-people/{id}/tips/{tip.Id}", tip.ToPublicResponse());
    }

    [HttpPost("{id:guid}/photo")]
    [RequestSizeLimit(MaxPhotoSize)]
    public async Task<ActionResult<MissingPersonPhotoResponse>> UploadPhoto(Guid id, [FromHeader(Name = "X-Management-Code")] string? code, IFormFile file, CancellationToken cancellationToken)
    {
        var person = await missingPeople.GetForUpdateAsync(id, includeLocations: false, cancellationToken);
        if (person is null)
        {
            return NotFound();
        }
        if (!Authorize(code, person))
        {
            return Unauthorized();
        }
        if (file.Length == 0 || file.Length > MaxPhotoSize ||
            file.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            return BadRequest("Only JPEG, PNG, and WebP images up to 10 MB are allowed.");
        }
        var extension = file.ContentType switch { "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg" };
        await using var stream = file.OpenReadStream();
        person.PhotoUrl = await photos.UploadAsync(id, extension, stream, file.ContentType, cancellationToken);
        person.UpdatedAt = DateTimeOffset.UtcNow;
        await missingPeople.PersistUpdateAsync(person, cancellationToken);
        return Ok(new MissingPersonPhotoResponse(person.PhotoUrl));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<MissingPersonResponse>> UpdateStatus(Guid id, UpdateMissingPersonStatusRequest request, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var person = await missingPeople.GetForUpdateAsync(id, includeLocations: false, cancellationToken);
        if (person is null)
        {
            return NotFound();
        }
        if (!Authorize(code, person))
        {
            return Unauthorized();
        }
        if (!Enum.IsDefined(request.Status))
        {
            return BadRequest("Invalid status.");
        }
        person.Status = request.Status;
        person.UpdatedAt = DateTimeOffset.UtcNow;
        await missingPeople.PersistUpdateAsync(person, cancellationToken);
        return Ok(person.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MissingPersonResponse>> Update(Guid id, UpdateMissingPersonRequest request, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Description) || request.Locations is null || request.Locations.Count == 0)
        {
            return BadRequest("Name, description, and at least one address are required.");
        }
        var person = await missingPeople.GetForUpdateAsync(id, includeLocations: true, cancellationToken);
        if (person is null)
        {
            return NotFound();
        }
        if (!Authorize(code, person))
        {
            return Unauthorized();
        }
        person.FullName = request.FullName.Trim();
        person.SearchName = SearchTextNormalizer.Normalize(request.FullName + " " + request.Aliases);
        person.Aliases = request.Aliases?.Trim();
        person.ApproximateAge = request.ApproximateAge?.Trim();
        person.Description = request.Description.Trim();
        person.PhysicalDescription = request.PhysicalDescription?.Trim();
        person.ClothingDescription = request.ClothingDescription?.Trim();
        person.LastSeenAt = request.LastSeenAt;
        person.UpdatedAt = DateTimeOffset.UtcNow;
        var locations = request.Locations.Select(location => new MissingPersonLocation
        {
            Id = Guid.NewGuid(),
            MissingPersonId = id,
            Address = location.Address.Trim(),
            SearchAddress = SearchTextNormalizer.Normalize(location.Address),
            Location = GeoPoint.FromCoordinates(location.Latitude, location.Longitude),
            Note = location.Note?.Trim()
        }).ToList();
        await missingPeople.ReplaceLocationsAsync(person, locations, cancellationToken);
        return Ok(person.ToResponse());
    }

    [HttpGet("{id:guid}/management/tips")]
    public async Task<ActionResult<IEnumerable<PrivateMissingPersonTipResponse>>> PrivateTips(Guid id, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var person = await missingPeople.GetForAuthorizationAsync(id, cancellationToken);
        if (person is null)
        {
            return NotFound();
        }
        if (!Authorize(code, person))
        {
            return Unauthorized();
        }
        var tips = await missingPeople.GetPrivateTipsAsync(id, cancellationToken);
        return Ok(tips.Select(tip => tip.ToPrivateResponse()));
    }

    [HttpPatch("{id:guid}/tips/{tipId:guid}/visibility")]
    public async Task<ActionResult> HideTip(Guid id, Guid tipId, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var person = await missingPeople.GetForAuthorizationAsync(id, cancellationToken);
        if (person is null)
        {
            return NotFound();
        }
        if (!Authorize(code, person))
        {
            return Unauthorized();
        }
        if (!await missingPeople.HideTipAsync(id, tipId, cancellationToken))
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/abuse-reports")]
    public async Task<ActionResult> Abuse(Guid id, AbuseReportRequest request, CancellationToken cancellationToken)
    {
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable)
        {
            return StatusCode(503, "Verification service unavailable.");
        }
        if (!challenge.Success)
        {
            return BadRequest("Human verification failed.");
        }
        if (!await missingPeople.ExistsAsync(id, cancellationToken))
        {
            return NotFound();
        }
        var report = new AbuseReport
        {
            Id = Guid.NewGuid(),
            MissingPersonId = id,
            Reason = request.Reason.Trim(),
            Details = request.Details?.Trim()
        };
        await missingPeople.CreateAbuseReportAsync(report, cancellationToken);
        return Accepted();
    }

    private bool Authorize(string? code, MissingPerson person) => !string.IsNullOrWhiteSpace(code) && MissingPersonSecurity.Matches(code, person.ManagementCodeHash);

    private static string? LastFour(string? value)
    {
        var normalized = value is null ? string.Empty : new(value.Where(char.IsLetterOrDigit).ToArray());
        return normalized.Length == 0 ? null : normalized[^Math.Min(4, normalized.Length)..];
    }
    private static string? ValidateCreate(CreateMissingPersonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length > 200)
        {
            return "Full name is required.";
        }
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 2000)
        {
            return "Description is required.";
        }
        if (request.Locations is null || request.Locations.Count == 0 || request.Locations.Any(location => string.IsNullOrWhiteSpace(location.Address)))
        {
            return "At least one address is required.";
        }
        if (request.IdentificationNumber is not null && request.IdentificationDocumentType is null)
        {
            return "Document type is required with an identification number.";
        }
        if (!request.PublicationConsent)
        {
            return "Publication consent is required.";
        }
        if (request.LastSeenAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return "Last-seen time cannot be in the future.";
        }
        return null;
    }
}
