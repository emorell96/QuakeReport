using Microsoft.AspNetCore.Mvc;
using QuakeReport.ApiService.CollectionPoints;
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
using StorageGenerics.Extensions;

namespace QuakeReport.ApiService.Controllers;

[ApiController]
[Route("api/collection-points")]
public class CollectionPointsController(
    ICollectionPointService collectionPoints,
    IActiveEarthquakeService earthquakes,
    ITurnstileValidator turnstile,
    IModerationKeyValidator moderationKey) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? query = null,
        [FromQuery] CollectionPointOperationalStatus? operationalStatus = null,
        [FromQuery] CollectionPointModerationStatus? moderationStatus = null,
        [FromQuery] CollectionPointSortOption sort = CollectionPointSortOption.Newest,
        [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationParameters.DefaultPageSize,
        CancellationToken cancellationToken = default,
        [FromQuery] double? latitude = null, [FromQuery] double? longitude = null)
    {
        if (!PaginationParameters.IsValid(page, pageSize) || !Enum.IsDefined(sort) ||
            (operationalStatus is not null && !Enum.IsDefined(operationalStatus.Value)) ||
            (moderationStatus is not null && !Enum.IsDefined(moderationStatus.Value)) ||
            (latitude.HasValue != longitude.HasValue) ||
            (latitude.HasValue && !GeoPoint.IsValid(latitude.Value, longitude!.Value)))
        {
            return BadRequest("Invalid collection-point query parameters.");
        }

        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        var criteria = new CollectionPointQueryCriteria(
            earthquake?.Id,
            query,
            operationalStatus,
            moderationStatus,
            sort,
            latitude,
            longitude);
        var ordered = collectionPoints.GetOrderedQuery(criteria);
        var projected = ordered.SelectOrdered(point => point.ToSummaryResponse());
        return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var point = await collectionPoints.GetPublicAsync(id, cancellationToken);
        if (point is null)
        {
            return NotFound();
        }

        var comments = await collectionPoints.GetRecentPublicCommentsAsync(id, 20, cancellationToken);
        return Ok(point.ToResponse(comments.Select(comment => comment.ToResponse()).ToList()));
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> Comments(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationParameters.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        if (!PaginationParameters.IsValid(page, pageSize))
        {
            return BadRequest("Invalid pagination.");
        }

        if (!await collectionPoints.PublicExistsAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var ordered = collectionPoints.GetPublicCommentsQuery(id);
        var projected = ordered.SelectOrdered(comment => comment.ToResponse());
        return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCollectionPointRequest request, CancellationToken cancellationToken)
    {
        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(error);
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
        var point = CreateEntity(request, earthquake.Id, CollectionPointSource.Community, CollectionPointModerationStatus.Pending, code);
        await collectionPoints.CreateAsync(point, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = point.Id }, new CreateCollectionPointResponse(point.ToResponse(), code));
    }

    [HttpPost("management/lookup")]
    public async Task<IActionResult> LookupManagementCode(CollectionPointManagementCodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ManagementCode))
        {
            return BadRequest("Management code is required.");
        }

        var hash = MissingPersonSecurity.HashManagementCode(request.ManagementCode);
        var point = await collectionPoints.GetByManagementCodeHashAsync(hash, cancellationToken);
        if (point is null)
        {
            return NotFound();
        }

        return Ok(point.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateCollectionPointRequest request, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var point = await collectionPoints.GetForUpdateAsync(id, cancellationToken);
        if (point is null)
        {
            return NotFound();
        }

        if (!Authorize(code, point))
        {
            return Unauthorized();
        }

        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        Apply(point, request);
        await collectionPoints.PersistUpdateAsync(point, cancellationToken);
        return Ok(point.ToResponse());
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, UpdateCollectionPointStatusRequest request, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var point = await collectionPoints.GetForUpdateAsync(id, cancellationToken);
        if (point is null)
        {
            return NotFound();
        }

        if (!Authorize(code, point))
        {
            return Unauthorized();
        }

        if (!Enum.IsDefined(request.Status))
        {
            return BadRequest("Invalid status.");
        }

        point.OperationalStatus = request.Status;
        point.UpdatedAt = DateTimeOffset.UtcNow;
        await collectionPoints.PersistUpdateAsync(point, cancellationToken);
        return Ok(point.ToResponse());
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> CreateComment(Guid id, CreateCollectionPointCommentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000)
        {
            return BadRequest("A comment is required.");
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
        if (!await collectionPoints.PublicExistsAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var comment = new CollectionPointComment
        {
            Id = Guid.NewGuid(),
            CollectionPointId = id,
            DisplayName = request.DisplayName?.Trim(),
            Message = request.Message.Trim()
        };
        await collectionPoints.CreateCommentAsync(comment, cancellationToken);
        return Created($"/api/collection-points/{id}/comments/{comment.Id}", comment.ToResponse());
    }

    [HttpPatch("{id:guid}/comments/{commentId:guid}/visibility")]
    public async Task<IActionResult> HideComment(Guid id, Guid commentId, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var point = await collectionPoints.GetForUpdateAsync(id, cancellationToken);
        if (point is null)
        {
            return NotFound();
        }

        if (!Authorize(code, point))
        {
            return Unauthorized();
        }

        if (!await collectionPoints.HideCommentAsync(id, commentId, cancellationToken))
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/abuse-reports")]
    public async Task<IActionResult> Abuse(Guid id, CollectionPointAbuseReportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
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
        if (!await collectionPoints.ExistsAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var report = new CollectionPointAbuseReport
        {
            Id = Guid.NewGuid(),
            CollectionPointId = id,
            Reason = request.Reason.Trim(),
            Details = request.Details?.Trim()
        };
        await collectionPoints.CreateAbuseReportAsync(report, cancellationToken);
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
            return BadRequest("Invalid pagination.");
        }

        var points = collectionPoints.GetPendingQuery();
        var projected = points.SelectOrdered(point => point.ToSummaryResponse());
        return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
    }

    [HttpPost("moderation/official")]
    public async Task<IActionResult> CreateOfficial(
        CreateCollectionPointRequest request,
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
            return UnprocessableEntity("No active earthquake is configured.");
        }

        var point = CreateEntity(request, earthquake.Id, CollectionPointSource.Official, CollectionPointModerationStatus.Approved, null);
        point.ModeratedAt = DateTimeOffset.UtcNow;
        point.ModeratedBy = moderator?.Trim();
        await collectionPoints.CreateAsync(point, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = point.Id }, point.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}")]
    public async Task<IActionResult> Moderate(
        Guid id,
        UpdateCollectionPointModerationRequest request,
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

        var point = await collectionPoints.GetForUpdateAsync(id, cancellationToken);
        if (point is null)
        {
            return NotFound();
        }

        point.ModerationStatus = request.Status;
        point.ModeratedAt = DateTimeOffset.UtcNow;
        point.ModeratedBy = moderator?.Trim();
        point.UpdatedAt = DateTimeOffset.UtcNow;
        await collectionPoints.PersistUpdateAsync(point, cancellationToken);
        return Ok(point.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}/comments/{commentId:guid}/visibility")]
    public async Task<IActionResult> ModerateComment(
        Guid id,
        Guid commentId,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }

        if (!await collectionPoints.HideCommentAsync(id, commentId, cancellationToken))
        {
            return NotFound();
        }

        return NoContent();
    }

    private static bool Authorize(string? code, CollectionPoint point) =>
        !string.IsNullOrWhiteSpace(code) && point.ManagementCodeHash is not null &&
        MissingPersonSecurity.Matches(code, point.ManagementCodeHash);

    private static CollectionPoint CreateEntity(
        CreateCollectionPointRequest request,
        Guid earthquakeId,
        CollectionPointSource source,
        CollectionPointModerationStatus moderation,
        string? code)
    {
        var point = new CollectionPoint
        {
            Id = Guid.NewGuid(),
            EarthquakeId = earthquakeId,
            Name = request.Name.Trim(),
            OrganizationName = request.OrganizationName?.Trim(),
            Address = request.Address.Trim(),
            Location = GeoPoint.FromCoordinates(request.Latitude, request.Longitude),
            Description = request.Description?.Trim(),
            NeedsSummary = request.NeedsSummary.Trim(),
            ReceivingInstructions = request.ReceivingInstructions.Trim(),
            ContactName = request.ContactName?.Trim(),
            ContactPhone = request.ContactPhone?.Trim(),
            ContactWhatsApp = request.ContactWhatsApp?.Trim(),
            ContactEmail = request.ContactEmail?.Trim(),
            EndsAt = request.EndsAt,
            Source = source,
            ModerationStatus = moderation,
            ManagementCodeHash = code is null ? null : MissingPersonSecurity.HashManagementCode(code)
        };
        point.SearchText = SearchTextNormalizer.Normalize(string.Join(' ', point.Name, point.OrganizationName, point.Address, point.NeedsSummary));
        return point;
    }

    private static void Apply(CollectionPoint point, UpdateCollectionPointRequest request)
    {
        point.Name = request.Name.Trim();
        point.OrganizationName = request.OrganizationName?.Trim();
        point.Address = request.Address.Trim();
        point.Location = GeoPoint.FromCoordinates(request.Latitude, request.Longitude);
        point.Description = request.Description?.Trim();
        point.NeedsSummary = request.NeedsSummary.Trim();
        point.ReceivingInstructions = request.ReceivingInstructions.Trim();
        point.ContactName = request.ContactName?.Trim();
        point.ContactPhone = request.ContactPhone?.Trim();
        point.ContactWhatsApp = request.ContactWhatsApp?.Trim();
        point.ContactEmail = request.ContactEmail?.Trim();
        point.EndsAt = request.EndsAt;
        point.SearchText = SearchTextNormalizer.Normalize(string.Join(' ', point.Name, point.OrganizationName, point.Address, point.NeedsSummary));
        point.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? Validate(CreateCollectionPointRequest request) =>
        !request.PrivacyConsent
            ? "Privacy consent is required."
            : ValidateCore(request.Name, request.Address, request.NeedsSummary, request.ReceivingInstructions, request.EndsAt);

    private static string? Validate(UpdateCollectionPointRequest request) =>
        ValidateCore(request.Name, request.Address, request.NeedsSummary, request.ReceivingInstructions, request.EndsAt);

    private static string? ValidateCore(string name, string address, string needs, string instructions, DateTimeOffset? endsAt)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return "Name is required.";
        }
        if (string.IsNullOrWhiteSpace(address) || address.Length > 400)
        {
            return "Address is required.";
        }
        if (string.IsNullOrWhiteSpace(needs) || needs.Length > 2000)
        {
            return "Current needs are required.";
        }
        if (string.IsNullOrWhiteSpace(instructions) || instructions.Length > 2000)
        {
            return "Receiving instructions are required.";
        }
        if (endsAt is not null && endsAt < DateTimeOffset.UtcNow.AddMinutes(-5))
        {
            return "End date cannot be in the past.";
        }
        return null;
    }

}
