using Microsoft.AspNetCore.Mvc;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.ApiService.Pagination;
using QuakeReport.ApiService.Security;
using QuakeReport.ApiService.Shelters;
using QuakeReport.ApiService.Text;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data.Models;
using QuakeReport.Data.Geospatial;
using StorageGenerics.Extensions;

namespace QuakeReport.ApiService.Controllers;

[ApiController]
[Route("api/shelters")]
public class SheltersController(
    IShelterService shelters,
    IActiveEarthquakeService earthquakes,
    ITurnstileValidator turnstile,
    IModerationKeyValidator moderationKey) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? query = null,
        [FromQuery] ShelterOperationalStatus? operationalStatus = null,
        [FromQuery] ShelterModerationStatus? moderationStatus = null,
        [FromQuery] ShelterSortOption sort = ShelterSortOption.Newest,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationParameters.DefaultPageSize,
        CancellationToken cancellationToken = default,
        [FromQuery] double? latitude = null,
        [FromQuery] double? longitude = null)
    {
        if (!PaginationParameters.IsValid(page, pageSize) || !Enum.IsDefined(sort) ||
            (operationalStatus is not null && !Enum.IsDefined(operationalStatus.Value)) ||
            (moderationStatus is not null && !Enum.IsDefined(moderationStatus.Value)) ||
            (latitude.HasValue != longitude.HasValue) ||
            (latitude.HasValue && !GeoPoint.IsValid(latitude.Value, longitude!.Value)))
        {
            return BadRequest("Invalid shelter query parameters.");
        }

        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        var criteria = new ShelterQueryCriteria(
            earthquake?.Id,
            query,
            operationalStatus,
            moderationStatus,
            sort,
            latitude,
            longitude);
        var ordered = shelters.GetOrderedQuery(criteria);
        var projected = ordered.SelectOrdered(shelter => shelter.ToSummaryResponse());
        return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var shelter = await shelters.GetPublicAsync(id, cancellationToken);
        return shelter is null ? NotFound() : Ok(shelter.ToResponse());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateShelterRequest request, CancellationToken cancellationToken)
    {
        var validation = Validate(request);
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

        var code = MissingPersonSecurity.CreateManagementCode();
        var shelter = CreateEntity(request, earthquake.Id, ShelterSource.Community, ShelterModerationStatus.Pending, code);
        await shelters.CreateAsync(shelter, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = shelter.Id }, new CreateShelterResponse(shelter.ToResponse(), code));
    }

    [HttpPost("management/lookup")]
    public async Task<IActionResult> LookupManagementCode(ShelterManagementCodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ManagementCode))
        {
            return BadRequest("Management code is required.");
        }
        var hash = MissingPersonSecurity.HashManagementCode(request.ManagementCode);
        var shelter = await shelters.GetByManagementCodeHashAsync(hash, cancellationToken);
        return shelter is null ? NotFound() : Ok(shelter.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateShelterRequest request,
        [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var shelter = await shelters.GetForUpdateAsync(id, cancellationToken);
        if (shelter is null)
        {
            return NotFound();
        }
        if (!Authorize(code, shelter))
        {
            return Unauthorized();
        }
        var validation = Validate(request);
        if (validation is not null)
        {
            return BadRequest(validation);
        }
        Apply(shelter, request);
        await shelters.PersistUpdateAsync(shelter, cancellationToken);
        return Ok(shelter.ToResponse());
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, UpdateShelterStatusRequest request,
        [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status))
        {
            return BadRequest("Invalid status.");
        }
        var shelter = await shelters.GetForUpdateAsync(id, cancellationToken);
        if (shelter is null)
        {
            return NotFound();
        }
        if (!Authorize(code, shelter))
        {
            return Unauthorized();
        }
        shelter.OperationalStatus = request.Status;
        shelter.UpdatedAt = DateTimeOffset.UtcNow;
        await shelters.PersistUpdateAsync(shelter, cancellationToken);
        return Ok(shelter.ToResponse());
    }

    [HttpPost("{id:guid}/abuse-reports")]
    public async Task<IActionResult> Abuse(Guid id, ShelterAbuseReportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 200)
        {
            return BadRequest("A reason is required.");
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
        if (!await shelters.PublicExistsAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var report = new ShelterAbuseReport
        {
            Id = Guid.NewGuid(),
            ShelterId = id,
            Reason = request.Reason.Trim(),
            Details = request.Details?.Trim()
        };
        await shelters.CreateAbuseReportAsync(report, cancellationToken);
        return Accepted();
    }

    [HttpGet("moderation/pending")]
    public async Task<IActionResult> Pending([FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationParameters.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }
        if (!PaginationParameters.IsValid(page, pageSize))
        {
            return BadRequest("Invalid pagination.");
        }
        var ordered = shelters.GetPendingQuery();
        var projected = ordered.SelectOrdered(shelter => shelter.ToSummaryResponse());
        return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
    }

    [HttpPost("moderation/official")]
    public async Task<IActionResult> CreateOfficial(CreateShelterRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        [FromHeader(Name = "X-Moderator-Email")] string? moderator,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }
        var validation = Validate(request);
        if (validation is not null)
        {
            return BadRequest(validation);
        }
        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null)
        {
            return UnprocessableEntity("No active earthquake is configured.");
        }
        var shelter = CreateEntity(request, earthquake.Id, ShelterSource.Official, ShelterModerationStatus.Approved, null);
        shelter.ModeratedAt = DateTimeOffset.UtcNow;
        shelter.ModeratedBy = moderator?.Trim();
        await shelters.CreateAsync(shelter, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = shelter.Id }, shelter.ToResponse());
    }

    [HttpPut("moderation/{id:guid}")]
    public async Task<IActionResult> ModeratorUpdate(Guid id, UpdateShelterRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key, CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }
        var validation = Validate(request);
        if (validation is not null)
        {
            return BadRequest(validation);
        }
        var shelter = await shelters.GetForUpdateAsync(id, cancellationToken);
        if (shelter is null)
        {
            return NotFound();
        }
        Apply(shelter, request);
        await shelters.PersistUpdateAsync(shelter, cancellationToken);
        return Ok(shelter.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}")]
    public async Task<IActionResult> Moderate(Guid id, UpdateShelterModerationRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        [FromHeader(Name = "X-Moderator-Email")] string? moderator,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }
        if (!Enum.IsDefined(request.Status))
        {
            return BadRequest("Invalid moderation status.");
        }
        var shelter = await shelters.GetForUpdateAsync(id, cancellationToken);
        if (shelter is null)
        {
            return NotFound();
        }
        shelter.ModerationStatus = request.Status;
        shelter.ModeratedAt = DateTimeOffset.UtcNow;
        shelter.ModeratedBy = moderator?.Trim();
        shelter.UpdatedAt = DateTimeOffset.UtcNow;
        await shelters.PersistUpdateAsync(shelter, cancellationToken);
        return Ok(shelter.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}/status")]
    public async Task<IActionResult> ModeratorStatus(Guid id, UpdateShelterStatusRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key, CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }
        if (!Enum.IsDefined(request.Status))
        {
            return BadRequest("Invalid status.");
        }
        var shelter = await shelters.GetForUpdateAsync(id, cancellationToken);
        if (shelter is null)
        {
            return NotFound();
        }
        shelter.OperationalStatus = request.Status;
        shelter.UpdatedAt = DateTimeOffset.UtcNow;
        await shelters.PersistUpdateAsync(shelter, cancellationToken);
        return Ok(shelter.ToResponse());
    }

    private static bool Authorize(string? code, Shelter shelter) =>
        !string.IsNullOrWhiteSpace(code) && shelter.ManagementCodeHash is not null && MissingPersonSecurity.Matches(code, shelter.ManagementCodeHash);

    private static Shelter CreateEntity(CreateShelterRequest request, Guid earthquakeId, ShelterSource source, ShelterModerationStatus moderation, string? code)
    {
        var shelter = new Shelter
        {
            Id = Guid.NewGuid(),
            EarthquakeId = earthquakeId,
            Source = source,
            ModerationStatus = moderation,
            ManagementCodeHash = code is null ? null : MissingPersonSecurity.HashManagementCode(code),
            Name = request.Name.Trim(),
            OrganizationName = request.OrganizationName?.Trim(),
            Address = request.Address.Trim(),
            Location = GeoPoint.FromCoordinates(request.Latitude, request.Longitude),
            Description = request.Description.Trim(),
            OperatingInstructions = request.OperatingInstructions.Trim(),
            ContactName = request.ContactName?.Trim(),
            ContactPhone = request.ContactPhone?.Trim(),
            ContactWhatsApp = request.ContactWhatsApp?.Trim(),
            ContactEmail = request.ContactEmail?.Trim(),
        };
        shelter.SearchText = SearchTextNormalizer.Normalize(string.Join(' ', shelter.Name, shelter.OrganizationName, shelter.Address, shelter.Description));
        return shelter;
    }

    private static void Apply(Shelter shelter, UpdateShelterRequest request)
    {
        shelter.Name = request.Name.Trim();
        shelter.OrganizationName = request.OrganizationName?.Trim();
        shelter.Address = request.Address.Trim();
        shelter.Location = GeoPoint.FromCoordinates(request.Latitude, request.Longitude);
        shelter.Description = request.Description.Trim();
        shelter.OperatingInstructions = request.OperatingInstructions.Trim();
        shelter.ContactName = request.ContactName?.Trim();
        shelter.ContactPhone = request.ContactPhone?.Trim();
        shelter.ContactWhatsApp = request.ContactWhatsApp?.Trim();
        shelter.ContactEmail = request.ContactEmail?.Trim();
        shelter.SearchText = SearchTextNormalizer.Normalize(string.Join(' ', shelter.Name, shelter.OrganizationName, shelter.Address, shelter.Description));
        shelter.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? Validate(CreateShelterRequest request) =>
        !request.PrivacyConsent ? "Privacy consent is required." : ValidateCore(request.Name, request.Address, request.Description, request.OperatingInstructions);
    private static string? Validate(UpdateShelterRequest request) =>
        ValidateCore(request.Name, request.Address, request.Description, request.OperatingInstructions);
    private static string? ValidateCore(string name, string address, string description, string instructions)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return "Name is required.";
        }
        if (string.IsNullOrWhiteSpace(address) || address.Length > 400)
        {
            return "Address is required.";
        }
        if (string.IsNullOrWhiteSpace(description) || description.Length > 3000)
        {
            return "Description is required.";
        }
        if (string.IsNullOrWhiteSpace(instructions) || instructions.Length > 2000)
        {
            return "Operating instructions are required.";
        }
        return null;
    }

}
