using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using QuakeReport.ApiService.BloodDonationCenters;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.ApiService.Pagination;
using QuakeReport.ApiService.Security;
using QuakeReport.ApiService.Text;
using QuakeReport.ApiService.Validation;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Core.Models.API;
using QuakeReport.Data.Geospatial;
using QuakeReport.Data.Models;
using StorageGenerics.Core.Models;
using StorageGenerics.Extensions;

namespace QuakeReport.ApiService.Controllers;

[ApiController, Route("api/blood-donation-centers")]
public class BloodDonationCentersController(
    IBloodDonationCenterService centers,
    IActiveEarthquakeService earthquakes,
    ITurnstileValidator turnstile,
    IModerationKeyValidator moderationKey,
    IValidator<PaginationRequest> paginationValidator,
    IValidator<PagedRequest<BloodDonationCenterSearchFilter>> searchValidator) : ControllerBase
{

    [HttpPost("search")]
    public async Task<ActionResult<PagedResult<BloodDonationCenterSummaryResponse>>> Search(
        [FromBody] PagedRequest<BloodDonationCenterSearchFilter> request,
        CancellationToken cancellationToken = default)
    {
        var validation = await searchValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToProblemDetails("Invalid blood-donation-center search."));
        }

        var filter = request.Filter!;
        var earthquakeId = await earthquakes.ResolveEarthquakeIdAsync(
            filter.EarthquakeId,
            cancellationToken);
        if (earthquakeId is null)
        {
            return UnprocessableEntity("No active earthquake is configured.");
        }

        var criteria = new BloodDonationCenterQueryCriteria(
            earthquakeId.Value,
            filter.SearchText,
            filter.CenterType,
            filter.OperationalStatus,
            filter.ModerationStatus,
            filter.BloodTypes,
            filter.Components,
            filter.Sort,
            filter.CenterPoint?.Latitude,
            filter.CenterPoint?.Longitude);
        var ordered = centers.GetOrderedQuery(criteria);
        var projected = ordered.SelectOrdered(center => center.ToSummaryResponse());
        var result = await projected.ToPagedResultAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<BloodDonationCenterSummaryResponse>>> List(
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

        var criteria = new BloodDonationCenterQueryCriteria(
            earthquakeId.Value,
            null,
            null,
            null,
            null,
            null,
            null,
            BloodDonationSortOption.Newest,
            null,
            null);
        var ordered = centers.GetOrderedQuery(criteria);
        var projected = ordered.SelectOrdered(center => center.ToSummaryResponse());
        var result = await projected.ToPagedResultAsync(
            pagination.Page,
            pagination.PageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BloodDonationCenterResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var center = await centers.GetPublicAsync(id, cancellationToken);
        if (center is null)
        {
            return NotFound();
        }

        var comments = await centers.GetRecentPublicCommentsAsync(id, 20, cancellationToken);

        return Ok(center.ToResponse(comments.Select(x => x.ToResponse()).ToList()));
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<ActionResult<PagedResult<BloodDonationCenterCommentResponse>>> Comments(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationParameters.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!PaginationParameters.IsValid(page, pageSize))
        {
            return BadRequest("Paginación inválida.");
        }
        if (!await centers.PublicExistsAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var ordered = centers.GetPublicCommentsQuery(id);
        var projected = ordered.SelectOrdered(x => x.ToResponse());
        return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<CreateBloodDonationCenterResponse>> Create(CreateBloodDonationCenterRequest request, CancellationToken cancellationToken)
    {
        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable)
        {
            return StatusCode(503, "El servicio de verificación no está disponible.");
        }
        if (!challenge.Success)
        {
            return BadRequest("La verificación humana falló.");
        }

        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null)
        {
            return UnprocessableEntity("No hay un terremoto activo configurado.");
        }

        var code = MissingPersonSecurity.CreateManagementCode();
        var center = CreateEntity(request, earthquake.Id, BloodDonationSource.Community, BloodDonationModerationStatus.Pending, code);
        await centers.CreateAsync(center, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = center.Id }, new CreateBloodDonationCenterResponse(center.ToResponse(), code));
    }

    [HttpPost("management/lookup")]
    public async Task<ActionResult<BloodDonationCenterResponse>> Lookup(BloodDonationCenterManagementCodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ManagementCode))
        {
            return BadRequest("El código es obligatorio.");
        }

        var hash = MissingPersonSecurity.HashManagementCode(request.ManagementCode);
        var center = await centers.GetByManagementCodeHashAsync(hash, cancellationToken);

        return center is null ? NotFound() : Ok(center.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BloodDonationCenterResponse>> Update(
        Guid id,
        UpdateBloodDonationCenterRequest request,
        [FromHeader(Name = "X-Management-Code")] string? code,
        CancellationToken cancellationToken)
    {
        var center = await centers.GetForUpdateAsync(id, cancellationToken);
        if (center is null)
        {
            return NotFound();
        }
        if (!Authorize(code, center))
        {
            return Unauthorized();
        }

        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        Apply(center, request);
        await centers.PersistUpdateAsync(center, cancellationToken);
        return Ok(center.ToResponse());
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<BloodDonationCenterResponse>> Status(
        Guid id,
        UpdateBloodDonationCenterStatusRequest request,
        [FromHeader(Name = "X-Management-Code")] string? code,
        CancellationToken cancellationToken)
    {
        var center = await centers.GetForUpdateAsync(id, cancellationToken);
        if (center is null)
        {
            return NotFound();
        }
        if (!Authorize(code, center))
        {
            return Unauthorized();
        }
        if (!Enum.IsDefined(request.Status))
        {
            return BadRequest("Estado inválido.");
        }

        center.OperationalStatus = request.Status;
        center.UpdatedAt = DateTimeOffset.UtcNow;
        await centers.PersistUpdateAsync(center, cancellationToken);
        return Ok(center.ToResponse());
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<BloodDonationCenterCommentResponse>> Comment(Guid id, CreateBloodDonationCenterCommentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000)
        {
            return BadRequest("El comentario es obligatorio.");
        }

        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable)
        {
            return StatusCode(503);
        }
        if (!challenge.Success)
        {
            return BadRequest("La verificación humana falló.");
        }

        if (!await centers.PublicExistsAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var comment = new BloodDonationCenterComment
        {
            Id = Guid.NewGuid(),
            BloodDonationCenterId = id,
            DisplayName = request.DisplayName?.Trim(),
            Message = request.Message.Trim()
        };
        await centers.CreateCommentAsync(comment, cancellationToken);

        return Created($"/api/blood-donation-centers/{id}/comments/{comment.Id}", comment.ToResponse());
    }

    [HttpPatch("{id:guid}/comments/{commentId:guid}/visibility")]
    public async Task<ActionResult> HideComment(
        Guid id,
        Guid commentId,
        [FromHeader(Name = "X-Management-Code")] string? code,
        CancellationToken cancellationToken)
    {
        var center = await centers.GetForUpdateAsync(id, cancellationToken);
        if (center is null)
        {
            return NotFound();
        }
        if (!Authorize(code, center))
        {
            return Unauthorized();
        }

        if (!await centers.HideCommentAsync(id, commentId, cancellationToken))
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/abuse-reports")]
    public async Task<ActionResult> Abuse(Guid id, BloodDonationCenterAbuseReportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest("El motivo es obligatorio.");
        }

        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable)
        {
            return StatusCode(503);
        }
        if (!challenge.Success)
        {
            return BadRequest("La verificación humana falló.");
        }

        if (!await centers.ExistsAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var report = new BloodDonationCenterAbuseReport
        {
            Id = Guid.NewGuid(),
            BloodDonationCenterId = id,
            Reason = request.Reason.Trim(),
            Details = request.Details?.Trim()
        };
        await centers.CreateAbuseReportAsync(report, cancellationToken);
        return Accepted();
    }

    [HttpGet("moderation/pending")]
    public async Task<ActionResult<PagedResult<BloodDonationCenterSummaryResponse>>> Pending(
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationParameters.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }
        if (!PaginationParameters.IsValid(page, pageSize))
        {
            return BadRequest("Paginación inválida.");
        }

        var pendingCenters = centers.GetPendingQuery();
        var projected = pendingCenters.SelectOrdered(x => x.ToSummaryResponse());
        return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
    }

    [HttpPost("moderation/official")]
    public async Task<ActionResult<BloodDonationCenterResponse>> Official(
        CreateBloodDonationCenterRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        [FromHeader(Name = "X-Moderator-Email")] string? moderator,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }

        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null)
        {
            return UnprocessableEntity();
        }

        var center = CreateEntity(request, earthquake.Id, BloodDonationSource.Official, BloodDonationModerationStatus.Approved, null);
        center.ModeratedAt = DateTimeOffset.UtcNow;
        center.ModeratedBy = moderator?.Trim();
        await centers.CreateAsync(center, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = center.Id }, center.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}")]
    public async Task<ActionResult<BloodDonationCenterResponse>> Moderate(
        Guid id,
        UpdateBloodDonationCenterModerationRequest request,
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
            return BadRequest();
        }

        var center = await centers.GetForUpdateAsync(id, cancellationToken);
        if (center is null)
        {
            return NotFound();
        }

        center.ModerationStatus = request.Status;
        center.ModeratedAt = DateTimeOffset.UtcNow;
        center.ModeratedBy = moderator?.Trim();
        center.UpdatedAt = DateTimeOffset.UtcNow;
        await centers.PersistUpdateAsync(center, cancellationToken);
        return Ok(center.ToResponse());
    }

    [HttpPut("moderation/{id:guid}")]
    public async Task<ActionResult<BloodDonationCenterResponse>> ModeratorUpdate(
        Guid id,
        UpdateBloodDonationCenterRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }

        var center = await centers.GetForUpdateAsync(id, cancellationToken);
        if (center is null)
        {
            return NotFound();
        }

        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        Apply(center, request);
        await centers.PersistUpdateAsync(center, cancellationToken);
        return Ok(center.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}/status")]
    public async Task<ActionResult<BloodDonationCenterResponse>> ModeratorStatus(
        Guid id,
        UpdateBloodDonationCenterStatusRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }

        var center = await centers.GetForUpdateAsync(id, cancellationToken);
        if (center is null)
        {
            return NotFound();
        }
        if (!Enum.IsDefined(request.Status))
        {
            return BadRequest();
        }

        center.OperationalStatus = request.Status;
        center.UpdatedAt = DateTimeOffset.UtcNow;
        await centers.PersistUpdateAsync(center, cancellationToken);
        return Ok(center.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}/comments/{commentId:guid}/visibility")]
    public async Task<ActionResult> ModeratorComment(
        Guid id,
        Guid commentId,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }

        if (!await centers.HideCommentAsync(id, commentId, cancellationToken))
        {
            return NotFound();
        }

        return NoContent();
    }


    private static bool Authorize(string? code, BloodDonationCenter center) =>
        !string.IsNullOrWhiteSpace(code) && center.ManagementCodeHash is not null &&
        MissingPersonSecurity.Matches(code, center.ManagementCodeHash);

    private static BloodDonationCenter CreateEntity(
        CreateBloodDonationCenterRequest r,
        Guid earthquakeId,
        BloodDonationSource source,
        BloodDonationModerationStatus moderation,
        string? code)
    {
        var c = new BloodDonationCenter
        {
            Id = Guid.NewGuid(),
            EarthquakeId = earthquakeId,
            Name = r.Name.Trim(),
            OrganizationName = r.OrganizationName?.Trim(),
            Address = r.Address.Trim(),
            Location = GeoPoint.FromCoordinates(r.Latitude, r.Longitude),
            Description = r.Description?.Trim(),
            OperatingInstructions = r.OperatingInstructions.Trim(),
            NeedsSummary = r.NeedsSummary.Trim(),
            PublicPhone = r.PublicPhone.Trim(),
            PublicWhatsApp = r.PublicWhatsApp?.Trim(),
            PublicEmail = r.PublicEmail?.Trim(),
            CenterType = r.CenterType,
            BloodTypes = r.BloodTypes,
            Components = r.Components,
            StartsAt = r.StartsAt,
            EndsAt = r.EndsAt,
            Source = source,
            ModerationStatus = moderation,
            ManagementCodeHash = code is null ? null : MissingPersonSecurity.HashManagementCode(code)
        };
        c.SearchText = SearchTextNormalizer.Normalize(string.Join(' ', c.Name, c.OrganizationName, c.Address, c.NeedsSummary, c.OperatingInstructions));
        return c;
    }

    private static void Apply(BloodDonationCenter c, UpdateBloodDonationCenterRequest r)
    {
        c.Name = r.Name.Trim();
        c.OrganizationName = r.OrganizationName?.Trim();
        c.Address = r.Address.Trim();
        c.Location = GeoPoint.FromCoordinates(r.Latitude, r.Longitude);
        c.Description = r.Description?.Trim();
        c.OperatingInstructions = r.OperatingInstructions.Trim();
        c.NeedsSummary = r.NeedsSummary.Trim();
        c.PublicPhone = r.PublicPhone.Trim();
        c.PublicWhatsApp = r.PublicWhatsApp?.Trim();
        c.PublicEmail = r.PublicEmail?.Trim();
        c.CenterType = r.CenterType;
        c.BloodTypes = r.BloodTypes;
        c.Components = r.Components;
        c.StartsAt = r.StartsAt;
        c.EndsAt = r.EndsAt;
        c.SearchText = SearchTextNormalizer.Normalize(string.Join(' ', c.Name, c.OrganizationName, c.Address, c.NeedsSummary, c.OperatingInstructions));
        c.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? Validate(CreateBloodDonationCenterRequest r) =>
        !r.PrivacyConsent
            ? "Privacy consent is required."
            : ValidateCore(r.Name, r.Address, r.OperatingInstructions, r.NeedsSummary, r.PublicPhone, r.PublicWhatsApp, r.CenterType, r.BloodTypes, r.Components, r.StartsAt, r.EndsAt);

    private static string? Validate(UpdateBloodDonationCenterRequest r) =>
        ValidateCore(r.Name, r.Address, r.OperatingInstructions, r.NeedsSummary, r.PublicPhone, r.PublicWhatsApp, r.CenterType, r.BloodTypes, r.Components, r.StartsAt, r.EndsAt);

    private static string? ValidateCore(
        string name,
        string address,
        string instructions,
        string needs,
        string phone,
        string? whatsapp,
        BloodDonationCenterType type,
        BloodTypeFlags types,
        BloodComponentFlags components,
        DateTimeOffset? starts,
        DateTimeOffset? ends)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return "El nombre es obligatorio.";
        }
        if (string.IsNullOrWhiteSpace(address) || address.Length > 400)
        {
            return "La dirección es obligatoria.";
        }
        if (string.IsNullOrWhiteSpace(instructions) || instructions.Length > 2500)
        {
            return "Las instrucciones son obligatorias.";
        }
        if (!Enum.IsDefined(type) || !ValidBloodTypes(types) || !ValidComponents(components))
        {
            return "Selecciona tipos de sangre y componentes válidos.";
        }
        if (type == BloodDonationCenterType.TemporaryCampaign && (starts is null || ends is null))
        {
            return "Las campañas requieren fecha de inicio y fin.";
        }
        if (starts is not null && ends is not null && ends < starts)
        {
            return "La fecha final no puede ser anterior a la inicial.";
        }
        return null;
    }

    private static bool ValidBloodTypes(BloodTypeFlags value)
    {
        const BloodTypeFlags validValues =
            BloodTypeFlags.APositive |
            BloodTypeFlags.ANegative |
            BloodTypeFlags.BPositive |
            BloodTypeFlags.BNegative |
            BloodTypeFlags.ABPositive |
            BloodTypeFlags.ABNegative |
            BloodTypeFlags.OPositive |
            BloodTypeFlags.ONegative |
            BloodTypeFlags.Unknown;

        return value != BloodTypeFlags.None && (value & ~validValues) == 0;
    }

    private static bool ValidComponents(BloodComponentFlags value)
    {
        const BloodComponentFlags validValues =
            BloodComponentFlags.WholeBlood |
            BloodComponentFlags.RedBloodCells |
            BloodComponentFlags.Plasma |
            BloodComponentFlags.Platelets |
            BloodComponentFlags.Unknown;

        return value != BloodComponentFlags.None && (value & ~validValues) == 0;
    }

}
