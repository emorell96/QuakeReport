using Microsoft.AspNetCore.Mvc;
using QuakeReport.ApiService.BloodDonationCenters;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.ApiService.Pagination;
using QuakeReport.ApiService.Security;
using QuakeReport.ApiService.Text;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data.Models;
using QuakeReport.Data.Geospatial;
using StorageGenerics.Core.Models;
using StorageGenerics.Extensions;

namespace QuakeReport.ApiService.Controllers;

[ApiController, Route("api/blood-donation-centers")]
public class BloodDonationCentersController(
    IBloodDonationCenterService centers,
    IActiveEarthquakeService earthquakes,
    ITurnstileValidator turnstile,
    IModerationKeyValidator moderationKey) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? query = null,
        [FromQuery] BloodDonationCenterType? centerType = null,
        [FromQuery] BloodDonationOperationalStatus? operationalStatus = null,
        [FromQuery] BloodDonationModerationStatus? moderationStatus = null,
        [FromQuery] BloodTypeFlags? bloodTypes = null,
        [FromQuery] BloodComponentFlags? components = null,
        [FromQuery] BloodDonationSortOption sort = BloodDonationSortOption.Newest,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationParameters.DefaultPageSize,
        CancellationToken cancellationToken = default,
        [FromQuery] double? latitude = null,
        [FromQuery] double? longitude = null)
    {
        if (!PaginationParameters.IsValid(page, pageSize) || !Enum.IsDefined(sort) ||
            (centerType is not null && !Enum.IsDefined(centerType.Value)) ||
            (operationalStatus is not null && !Enum.IsDefined(operationalStatus.Value)) ||
            (moderationStatus is not null && !Enum.IsDefined(moderationStatus.Value)) ||
            !ValidBloodTypes(bloodTypes) || !ValidComponents(components))
        {
            return BadRequest("Parámetros inválidos.");
        }

        if ((latitude.HasValue != longitude.HasValue) ||
            (latitude.HasValue && !GeoPoint.IsValid(latitude.Value, longitude!.Value)))
        {
            return BadRequest("Coordenadas inválidas.");
        }

        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        var criteria = new BloodDonationCenterQueryCriteria(
            earthquake?.Id,
            query,
            centerType,
            operationalStatus,
            moderationStatus,
            bloodTypes,
            components,
            sort,
            latitude,
            longitude);
        var ordered = centers.GetOrderedQuery(criteria);
        var projected = ordered.SelectOrdered(x => x.ToSummaryResponse());
        return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
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
    public async Task<IActionResult> Comments(
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
    public async Task<IActionResult> Create(CreateBloodDonationCenterRequest request, CancellationToken cancellationToken)
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
    public async Task<IActionResult> Lookup(BloodDonationCenterManagementCodeRequest request, CancellationToken cancellationToken)
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
    public async Task<IActionResult> Update(
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
    public async Task<IActionResult> Status(
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
    public async Task<IActionResult> Comment(Guid id, CreateBloodDonationCenterCommentRequest request, CancellationToken cancellationToken)
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
    public async Task<IActionResult> HideComment(
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
    public async Task<IActionResult> Abuse(Guid id, BloodDonationCenterAbuseReportRequest request, CancellationToken cancellationToken)
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
    public async Task<IActionResult> Pending(
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
    public async Task<IActionResult> Official(
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
    public async Task<IActionResult> Moderate(
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
    public async Task<IActionResult> ModeratorUpdate(
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
    public async Task<IActionResult> ModeratorStatus(
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
    public async Task<IActionResult> ModeratorComment(
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

    private static bool ValidBloodTypes(BloodTypeFlags? value) =>
        value is null || value.Value != BloodTypeFlags.None && (value.Value & ~(
            BloodTypeFlags.APositive | BloodTypeFlags.ANegative |
            BloodTypeFlags.BPositive | BloodTypeFlags.BNegative |
            BloodTypeFlags.ABPositive | BloodTypeFlags.ABNegative |
            BloodTypeFlags.OPositive | BloodTypeFlags.ONegative |
            BloodTypeFlags.Unknown)) == 0;

    private static bool ValidComponents(BloodComponentFlags? value) =>
        value is null || value.Value != BloodComponentFlags.None && (value.Value & ~(
            BloodComponentFlags.WholeBlood | BloodComponentFlags.RedBloodCells |
            BloodComponentFlags.Plasma | BloodComponentFlags.Platelets |
            BloodComponentFlags.Unknown)) == 0;

}
