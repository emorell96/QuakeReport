using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.HelpRequests;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.ApiService.Pagination;
using QuakeReport.ApiService.Security;
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
[Route("api/help-requests")]
public class HelpRequestsController(
    IHelpRequestService helpRequests,
    IActiveEarthquakeService earthquakes,
    ITurnstileValidator turnstile,
    IModerationKeyValidator moderationKey,
    IValidator<PaginationRequest> paginationValidator,
    IValidator<PagedRequest<HelpRequestSearchFilter>> searchValidator) : ControllerBase
{
    private const HelpNeedCategory AllCategories = HelpNeedCategory.Personnel | HelpNeedCategory.Medical |
        HelpNeedCategory.RescueEquipment | HelpNeedCategory.Machinery | HelpNeedCategory.Transportation |
        HelpNeedCategory.FoodAndWater | HelpNeedCategory.Communications | HelpNeedCategory.TemporaryShelter |
        HelpNeedCategory.Security | HelpNeedCategory.Other;

    [HttpGet]
    public async Task<ActionResult<PagedResult<HelpRequestSummaryResponse>>> List(
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

        var criteria = new HelpRequestQueryCriteria(
            earthquakeId.Value,
            null,
            null,
            null,
            null,
            null,
            HelpRequestSortOption.HighestPriority,
            null,
            null);
        var ordered = helpRequests.GetOrderedQuery(criteria);
        var projected = ordered.SelectOrdered(item => item.ToSummaryResponse());
        var result = await projected.ToPagedResultAsync(
            pagination.Page,
            pagination.PageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<ActionResult<PagedResult<HelpRequestSummaryResponse>>> Search(
        [FromBody] PagedRequest<HelpRequestSearchFilter> request,
        CancellationToken cancellationToken = default)
    {
        var validation = await searchValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToProblemDetails("Invalid help-request search."));
        }

        var filter = request.Filter!;
        var earthquakeId = await earthquakes.ResolveEarthquakeIdAsync(
            filter.EarthquakeId,
            cancellationToken);
        if (earthquakeId is null)
        {
            return UnprocessableEntity("No active earthquake is configured.");
        }

        var criteria = new HelpRequestQueryCriteria(
            earthquakeId.Value,
            filter.SearchText,
            filter.Priority,
            filter.Category,
            filter.Status,
            filter.ModerationStatus,
            filter.Sort,
            filter.CenterPoint?.Latitude,
            filter.CenterPoint?.Longitude);
        var ordered = helpRequests.GetOrderedQuery(criteria);
        var projected = ordered.SelectOrdered(item => item.ToSummaryResponse());
        var result = await projected.ToPagedResultAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HelpRequestResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var request = await helpRequests.GetPublicAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound();
        }
        var comments = await helpRequests.GetRecentPublicCommentsAsync(id, 20, cancellationToken);
        return Ok(request.ToResponse(comments.Select(comment => comment.ToResponse()).ToList()));
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<ActionResult<PagedResult<HelpRequestCommentResponse>>> Comments(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = PaginationParameters.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        if (!PaginationParameters.IsValid(page, pageSize))
        {
            return BadRequest("Invalid pagination.");
        }
        var request = await helpRequests.GetPublicAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound();
        }
        var ordered = helpRequests.GetPublicCommentsQuery(id);
        var projected = ordered.SelectOrdered(comment => comment.ToResponse());
        return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<CreateHelpRequestResponse>> Create(CreateHelpRequestRequest request, CancellationToken cancellationToken)
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
        var helpRequest = CreateEntity(request, earthquake.Id, HelpRequestSource.Community, HelpRequestModerationStatus.Pending, code);
        await helpRequests.CreateAsync(helpRequest, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = helpRequest.Id }, new CreateHelpRequestResponse(helpRequest.ToResponse(), code));
    }

    [HttpPost("management/lookup")]
    public async Task<ActionResult<HelpRequestResponse>> LookupManagementCode(HelpRequestManagementCodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ManagementCode))
        {
            return BadRequest("Management code is required.");
        }
        var hash = MissingPersonSecurity.HashManagementCode(request.ManagementCode);
        var helpRequest = await helpRequests.GetByManagementCodeHashAsync(hash, cancellationToken);
        return helpRequest is null ? NotFound() : Ok(helpRequest.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<HelpRequestResponse>> Update(Guid id, UpdateHelpRequestRequest request, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var helpRequest = await helpRequests.GetForUpdateAsync(id, cancellationToken);
        if (helpRequest is null)
        {
            return NotFound();
        }
        if (!Authorize(code, helpRequest))
        {
            return Unauthorized();
        }
        var error = Validate(request);
        if (error is not null)
        {
            return BadRequest(error);
        }
        Apply(helpRequest, request);
        await helpRequests.PersistUpdateAsync(helpRequest, cancellationToken);
        return Ok(helpRequest.ToResponse());
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<HelpRequestResponse>> Status(Guid id, UpdateHelpRequestStatusRequest request, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status))
        {
            return BadRequest("Invalid status.");
        }
        var helpRequest = await helpRequests.GetForUpdateAsync(id, cancellationToken);
        if (helpRequest is null)
        {
            return NotFound();
        }
        if (!Authorize(code, helpRequest))
        {
            return Unauthorized();
        }
        helpRequest.Status = request.Status;
        helpRequest.UpdatedAt = DateTimeOffset.UtcNow;
        await helpRequests.PersistUpdateAsync(helpRequest, cancellationToken);
        return Ok(helpRequest.ToResponse());
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<HelpRequestCommentResponse>> CreateComment(Guid id, CreateHelpRequestCommentRequest request, CancellationToken cancellationToken)
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
        var helpRequest = await helpRequests.GetPublicAsync(id, cancellationToken);
        if (helpRequest is null)
        {
            return NotFound();
        }
        if (helpRequest.Status == HelpRequestStatus.Resolved)
        {
            return Conflict("This request is resolved.");
        }
        var comment = new HelpRequestComment
        {
            Id = Guid.NewGuid(),
            HelpRequestId = id,
            DisplayName = request.DisplayName?.Trim(),
            Message = request.Message.Trim()
        };
        await helpRequests.CreateCommentAsync(comment, cancellationToken);
        return Created($"/api/help-requests/{id}/comments/{comment.Id}", comment.ToResponse());
    }

    [HttpPatch("{id:guid}/comments/{commentId:guid}/visibility")]
    public async Task<ActionResult> HideComment(Guid id, Guid commentId, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var helpRequest = await helpRequests.GetForUpdateAsync(id, cancellationToken);
        if (helpRequest is null)
        {
            return NotFound();
        }
        if (!Authorize(code, helpRequest))
        {
            return Unauthorized();
        }
        if (!await helpRequests.HideCommentAsync(id, commentId, cancellationToken))
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("{id:guid}/abuse-reports")]
    public async Task<ActionResult> Abuse(Guid id, HelpRequestAbuseReportRequest request, CancellationToken cancellationToken)
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
        if (!await helpRequests.ExistsAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var report = new HelpRequestAbuseReport
        {
            Id = Guid.NewGuid(),
            HelpRequestId = id,
            Reason = request.Reason.Trim(),
            Details = request.Details?.Trim()
        };
        await helpRequests.CreateAbuseReportAsync(report, cancellationToken);
        return Accepted();
    }


    [HttpGet("moderation/pending")]
    public async Task<ActionResult<PagedResult<HelpRequestSummaryResponse>>> Pending(
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

        var ordered = helpRequests.GetPendingQuery();
        var projected = ordered.SelectOrdered(item => item.ToSummaryResponse());
        return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
    }

    [HttpPost("moderation/official")]
    public async Task<ActionResult<HelpRequestResponse>> CreateOfficial(
        CreateHelpRequestRequest request,
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

        var helpRequest = CreateEntity(request, earthquake.Id, HelpRequestSource.Official, HelpRequestModerationStatus.Approved, null);
        helpRequest.ModeratedAt = DateTimeOffset.UtcNow;
        helpRequest.ModeratedBy = moderator?.Trim();
        await helpRequests.CreateAsync(helpRequest, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = helpRequest.Id }, helpRequest.ToResponse());
    }

    [HttpPut("moderation/{id:guid}")]
    public async Task<ActionResult<HelpRequestResponse>> ModeratorUpdate(
        Guid id,
        UpdateHelpRequestRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
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

        var helpRequest = await helpRequests.GetForUpdateAsync(id, cancellationToken);
        if (helpRequest is null)
        {
            return NotFound();
        }

        Apply(helpRequest, request);
        await helpRequests.PersistUpdateAsync(helpRequest, cancellationToken);
        return Ok(helpRequest.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}")]
    public async Task<ActionResult<HelpRequestResponse>> Moderate(
        Guid id,
        UpdateHelpRequestModerationRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        [FromHeader(Name = "X-Moderator-Email")] string? moderator,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key) || !Enum.IsDefined(request.Status))
        {
            return Unauthorized();
        }

        var helpRequest = await helpRequests.GetForUpdateAsync(id, cancellationToken);
        if (helpRequest is null)
        {
            return NotFound();
        }

        helpRequest.ModerationStatus = request.Status;
        helpRequest.ModeratedAt = DateTimeOffset.UtcNow;
        helpRequest.ModeratedBy = moderator?.Trim();
        helpRequest.UpdatedAt = DateTimeOffset.UtcNow;
        await helpRequests.PersistUpdateAsync(helpRequest, cancellationToken);
        return Ok(helpRequest.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}/status")]
    public async Task<ActionResult<HelpRequestResponse>> ModeratorStatus(
        Guid id,
        UpdateHelpRequestStatusRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key) || !Enum.IsDefined(request.Status))
        {
            return Unauthorized();
        }

        var helpRequest = await helpRequests.GetForUpdateAsync(id, cancellationToken);
        if (helpRequest is null)
        {
            return NotFound();
        }

        helpRequest.Status = request.Status;
        helpRequest.UpdatedAt = DateTimeOffset.UtcNow;
        await helpRequests.PersistUpdateAsync(helpRequest, cancellationToken);
        return Ok(helpRequest.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}/comments/{commentId:guid}/visibility")]
    public async Task<ActionResult> ModerateComment(
        Guid id,
        Guid commentId,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }

        if (!await helpRequests.HideCommentAsync(id, commentId, cancellationToken))
        {
            return NotFound();
        }
        return NoContent();
    }

    private static bool Authorize(string? code, HelpRequest request) =>
        !string.IsNullOrWhiteSpace(code) && request.ManagementCodeHash is not null &&
        MissingPersonSecurity.Matches(code, request.ManagementCodeHash);

    private static bool ValidCategories(HelpNeedCategory value) =>
        value != HelpNeedCategory.None && (value & ~AllCategories) == 0;

    private static HelpRequest CreateEntity(
        CreateHelpRequestRequest request,
        Guid earthquakeId,
        HelpRequestSource source,
        HelpRequestModerationStatus moderation,
        string? code)
    {
        var item = new HelpRequest
        {
            Id = Guid.NewGuid(),
            EarthquakeId = earthquakeId,
            Title = request.Title.Trim(),
            RequesterName = request.RequesterName.Trim(),
            OrganizationName = request.OrganizationName?.Trim(),
            Address = request.Address.Trim(),
            Location = GeoPoint.FromCoordinates(request.Latitude, request.Longitude),
            NeedDetails = request.NeedDetails.Trim(),
            Instructions = request.Instructions?.Trim(),
            PublicPhone = request.PublicPhone.Trim(),
            PublicWhatsApp = request.PublicWhatsApp?.Trim(),
            PublicEmail = request.PublicEmail?.Trim(),
            Priority = request.Priority,
            NeedCategories = request.NeedCategories,
            NeededBy = request.NeededBy,
            Source = source,
            ModerationStatus = moderation,
            ManagementCodeHash = code is null ? null : MissingPersonSecurity.HashManagementCode(code)
        };
        item.SearchText = SearchTextNormalizer.Normalize(string.Join(' ', item.Title, item.RequesterName, item.OrganizationName, item.Address, item.NeedDetails, item.Instructions));
        return item;
    }

    private static void Apply(HelpRequest item, UpdateHelpRequestRequest request)
    {
        item.Title = request.Title.Trim();
        item.RequesterName = request.RequesterName.Trim();
        item.OrganizationName = request.OrganizationName?.Trim();
        item.Address = request.Address.Trim();
        item.Location = GeoPoint.FromCoordinates(request.Latitude, request.Longitude);
        item.NeedDetails = request.NeedDetails.Trim();
        item.Instructions = request.Instructions?.Trim();
        item.PublicPhone = request.PublicPhone.Trim();
        item.PublicWhatsApp = request.PublicWhatsApp?.Trim();
        item.PublicEmail = request.PublicEmail?.Trim();
        item.Priority = request.Priority;
        item.NeedCategories = request.NeedCategories;
        item.NeededBy = request.NeededBy;
        item.SearchText = SearchTextNormalizer.Normalize(string.Join(' ', item.Title, item.RequesterName, item.OrganizationName, item.Address, item.NeedDetails, item.Instructions));
        item.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? Validate(CreateHelpRequestRequest request) =>
        !request.PublicContactConsent
            ? "Public contact consent is required."
            : ValidateCore(request.Title, request.RequesterName, request.Address, request.NeedDetails, request.PublicPhone, request.PublicWhatsApp, request.Priority, request.NeedCategories);

    private static string? Validate(UpdateHelpRequestRequest request) =>
        ValidateCore(request.Title, request.RequesterName, request.Address, request.NeedDetails, request.PublicPhone, request.PublicWhatsApp, request.Priority, request.NeedCategories);

    private static string? ValidateCore(
        string title,
        string requester,
        string address,
        string details,
        string phone,
        string? whatsApp,
        HelpRequestPriority priority,
        HelpNeedCategory categories)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            return "Title is required.";
        }
        if (string.IsNullOrWhiteSpace(requester) || requester.Length > 200)
        {
            return "Requester name is required.";
        }
        if (string.IsNullOrWhiteSpace(address) || address.Length > 400)
        {
            return "Address is required.";
        }
        if (string.IsNullOrWhiteSpace(details) || details.Length > 3000)
        {
            return "Need details are required.";
        }
        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(whatsApp))
        {
            return "A public phone or WhatsApp number is required.";
        }
        if (!Enum.IsDefined(priority))
        {
            return "Invalid priority.";
        }
        if (!ValidCategories(categories))
        {
            return "At least one need category is required.";
        }
        return null;
    }

}
