using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;

namespace QuakeReport.ApiService.Controllers;

[ApiController]
[Route("api/help-requests")]
public class HelpRequestsController(
    QuakeReportDbContext db,
    ActiveEarthquakeService earthquakes,
    ITurnstileValidator turnstile,
    IConfiguration configuration) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const HelpNeedCategory AllCategories = HelpNeedCategory.Personnel | HelpNeedCategory.Medical |
        HelpNeedCategory.RescueEquipment | HelpNeedCategory.Machinery | HelpNeedCategory.Transportation |
        HelpNeedCategory.FoodAndWater | HelpNeedCategory.Communications | HelpNeedCategory.TemporaryShelter |
        HelpNeedCategory.Security | HelpNeedCategory.Other;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? query = null,
        [FromQuery] HelpRequestPriority? priority = null, [FromQuery] HelpNeedCategory? category = null,
        [FromQuery] HelpRequestStatus? status = null, [FromQuery] HelpRequestModerationStatus? moderationStatus = null,
        [FromQuery] HelpRequestSortOption sort = HelpRequestSortOption.HighestPriority,
        [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize || !Enum.IsDefined(sort) ||
            (priority is not null && !Enum.IsDefined(priority.Value)) ||
            (status is not null && !Enum.IsDefined(status.Value)) ||
            (moderationStatus is not null && !Enum.IsDefined(moderationStatus.Value)) ||
            (category is not null && !ValidCategories(category.Value)))
            return BadRequest("Invalid help-request query parameters.");

        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null) return Ok(new PagedResponse<HelpRequestSummaryResponse>([], page, pageSize, 0, 0));

        var requests = db.HelpRequests.AsNoTracking().Where(request =>
            request.EarthquakeId == earthquake.Id && request.ModerationStatus != HelpRequestModerationStatus.Rejected);
        if (status is null) requests = requests.Where(request => request.Status != HelpRequestStatus.Resolved);
        else requests = requests.Where(request => request.Status == status);
        if (priority is not null) requests = requests.Where(request => request.Priority == priority);
        if (category is not null) requests = requests.Where(request => ((int)request.NeedCategories & (int)category.Value) != 0);
        if (moderationStatus is not null) requests = requests.Where(request => request.ModerationStatus == moderationStatus);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = NormalizeSearch(query);
            requests = requests.Where(request => request.SearchText!.Contains(normalized));
        }

        var total = await requests.CountAsync(cancellationToken);
        var ordered = sort switch
        {
            HelpRequestSortOption.Newest => requests.OrderByDescending(request => request.CreatedAt).ThenByDescending(request => request.Id),
            HelpRequestSortOption.RecentlyUpdated => requests.OrderByDescending(request => request.UpdatedAt).ThenByDescending(request => request.Id),
            HelpRequestSortOption.NeededSoon => requests.OrderBy(request => request.NeededBy == null).ThenBy(request => request.NeededBy).ThenByDescending(request => request.Priority).ThenBy(request => request.Id),
            _ => requests.OrderByDescending(request => request.Priority).ThenByDescending(request => request.CreatedAt).ThenByDescending(request => request.Id),
        };
        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new PagedResponse<HelpRequestSummaryResponse>(items.Select(request => request.ToSummaryResponse()).ToList(), page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var request = await db.HelpRequests.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.ModerationStatus != HelpRequestModerationStatus.Rejected, cancellationToken);
        if (request is null) return NotFound();
        var comments = await db.HelpRequestComments.AsNoTracking().Where(comment => comment.HelpRequestId == id && !comment.IsHidden).OrderByDescending(comment => comment.CreatedAt).Take(20).ToListAsync(cancellationToken);
        return Ok(request.ToResponse(comments.Select(comment => comment.ToResponse()).ToList()));
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> Comments(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize) return BadRequest("Invalid pagination.");
        var request = await db.HelpRequests.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.ModerationStatus != HelpRequestModerationStatus.Rejected, cancellationToken);
        if (request is null) return NotFound();
        var comments = db.HelpRequestComments.AsNoTracking().Where(comment => comment.HelpRequestId == id && !comment.IsHidden);
        var total = await comments.CountAsync(cancellationToken);
        var items = await comments.OrderByDescending(comment => comment.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new PagedResponse<HelpRequestCommentResponse>(items.Select(comment => comment.ToResponse()).ToList(), page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateHelpRequestRequest request, CancellationToken cancellationToken)
    {
        var error = Validate(request);
        if (error is not null) return BadRequest(error);
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable) return StatusCode(503, "Verification service unavailable.");
        if (!challenge.Success) return BadRequest("Human verification failed.");
        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null) return UnprocessableEntity("No active earthquake is configured.");
        var code = MissingPersonSecurity.CreateManagementCode();
        var helpRequest = CreateEntity(request, earthquake.Id, HelpRequestSource.Community, HelpRequestModerationStatus.Pending, code);
        db.HelpRequests.Add(helpRequest);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = helpRequest.Id }, new CreateHelpRequestResponse(helpRequest.ToResponse(), code));
    }

    [HttpPost("management/lookup")]
    public async Task<IActionResult> LookupManagementCode(HelpRequestManagementCodeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ManagementCode)) return BadRequest("Management code is required.");
        var hash = MissingPersonSecurity.HashManagementCode(request.ManagementCode);
        var helpRequest = await db.HelpRequests.AsNoTracking().SingleOrDefaultAsync(item => item.ManagementCodeHash == hash && item.ModerationStatus != HelpRequestModerationStatus.Rejected, cancellationToken);
        return helpRequest is null ? NotFound() : Ok(helpRequest.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateHelpRequestRequest request, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var helpRequest = await db.HelpRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (helpRequest is null) return NotFound();
        if (!Authorize(code, helpRequest)) return Unauthorized();
        var error = Validate(request);
        if (error is not null) return BadRequest(error);
        Apply(helpRequest, request);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(helpRequest.ToResponse());
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, UpdateHelpRequestStatusRequest request, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status)) return BadRequest("Invalid status.");
        var helpRequest = await db.HelpRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (helpRequest is null) return NotFound();
        if (!Authorize(code, helpRequest)) return Unauthorized();
        helpRequest.Status = request.Status; helpRequest.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(helpRequest.ToResponse());
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> CreateComment(Guid id, CreateHelpRequestCommentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000) return BadRequest("A comment is required.");
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable) return StatusCode(503, "Verification service unavailable.");
        if (!challenge.Success) return BadRequest("Human verification failed.");
        var helpRequest = await db.HelpRequests.SingleOrDefaultAsync(item => item.Id == id && item.ModerationStatus != HelpRequestModerationStatus.Rejected, cancellationToken);
        if (helpRequest is null) return NotFound();
        if (helpRequest.Status == HelpRequestStatus.Resolved) return Conflict("This request is resolved.");
        var comment = new HelpRequestComment { Id = Guid.NewGuid(), HelpRequestId = id, DisplayName = request.DisplayName?.Trim(), Message = request.Message.Trim() };
        db.HelpRequestComments.Add(comment); await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/help-requests/{id}/comments/{comment.Id}", comment.ToResponse());
    }

    [HttpPatch("{id:guid}/comments/{commentId:guid}/visibility")]
    public async Task<IActionResult> HideComment(Guid id, Guid commentId, [FromHeader(Name = "X-Management-Code")] string? code, CancellationToken cancellationToken)
    {
        var helpRequest = await db.HelpRequests.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (helpRequest is null) return NotFound();
        if (!Authorize(code, helpRequest)) return Unauthorized();
        var comment = await db.HelpRequestComments.SingleOrDefaultAsync(item => item.Id == commentId && item.HelpRequestId == id, cancellationToken);
        if (comment is null) return NotFound(); comment.IsHidden = true; await db.SaveChangesAsync(cancellationToken); return NoContent();
    }

    [HttpPost("{id:guid}/abuse-reports")]
    public async Task<IActionResult> Abuse(Guid id, HelpRequestAbuseReportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 200) return BadRequest("A reason is required.");
        var challenge = await turnstile.ValidateAsync(request.TurnstileToken, cancellationToken);
        if (challenge.ProviderUnavailable) return StatusCode(503, "Verification service unavailable.");
        if (!challenge.Success) return BadRequest("Human verification failed.");
        if (!await db.HelpRequests.AnyAsync(item => item.Id == id, cancellationToken)) return NotFound();
        db.HelpRequestAbuseReports.Add(new HelpRequestAbuseReport { Id = Guid.NewGuid(), HelpRequestId = id, Reason = request.Reason.Trim(), Details = request.Details?.Trim() });
        await db.SaveChangesAsync(cancellationToken); return Accepted();
    }

    [HttpGet("moderation/pending")]
    public async Task<IActionResult> Pending([FromHeader(Name = "X-Moderation-Service-Key")] string? key, [FromQuery] int page = 1, [FromQuery] int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        if (!Moderated(key)) return Unauthorized(); if (page < 1 || pageSize is < 1 or > MaxPageSize) return BadRequest("Invalid pagination.");
        var requests = db.HelpRequests.AsNoTracking().Where(item => item.ModerationStatus == HelpRequestModerationStatus.Pending);
        var total = await requests.CountAsync(cancellationToken); var items = await requests.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Ok(new PagedResponse<HelpRequestSummaryResponse>(items.Select(item => item.ToSummaryResponse()).ToList(), page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }

    [HttpPost("moderation/official")]
    public async Task<IActionResult> CreateOfficial(CreateHelpRequestRequest request, [FromHeader(Name = "X-Moderation-Service-Key")] string? key, [FromHeader(Name = "X-Moderator-Email")] string? moderator, CancellationToken cancellationToken)
    {
        if (!Moderated(key)) return Unauthorized(); var error = Validate(request); if (error is not null) return BadRequest(error);
        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken); if (earthquake is null) return UnprocessableEntity("No active earthquake is configured.");
        var helpRequest = CreateEntity(request, earthquake.Id, HelpRequestSource.Official, HelpRequestModerationStatus.Approved, null); helpRequest.ModeratedAt = DateTimeOffset.UtcNow; helpRequest.ModeratedBy = moderator?.Trim();
        db.HelpRequests.Add(helpRequest); await db.SaveChangesAsync(cancellationToken); return CreatedAtAction(nameof(Get), new { id = helpRequest.Id }, helpRequest.ToResponse());
    }

    [HttpPut("moderation/{id:guid}")]
    public async Task<IActionResult> ModeratorUpdate(Guid id, UpdateHelpRequestRequest request, [FromHeader(Name = "X-Moderation-Service-Key")] string? key, CancellationToken cancellationToken)
    {
        if (!Moderated(key)) return Unauthorized(); var error = Validate(request); if (error is not null) return BadRequest(error);
        var helpRequest = await db.HelpRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken); if (helpRequest is null) return NotFound(); Apply(helpRequest, request); await db.SaveChangesAsync(cancellationToken); return Ok(helpRequest.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}")]
    public async Task<IActionResult> Moderate(Guid id, UpdateHelpRequestModerationRequest request, [FromHeader(Name = "X-Moderation-Service-Key")] string? key, [FromHeader(Name = "X-Moderator-Email")] string? moderator, CancellationToken cancellationToken)
    {
        if (!Moderated(key) || !Enum.IsDefined(request.Status)) return Unauthorized(); var helpRequest = await db.HelpRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken); if (helpRequest is null) return NotFound();
        helpRequest.ModerationStatus = request.Status; helpRequest.ModeratedAt = DateTimeOffset.UtcNow; helpRequest.ModeratedBy = moderator?.Trim(); helpRequest.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(cancellationToken); return Ok(helpRequest.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}/status")]
    public async Task<IActionResult> ModeratorStatus(Guid id, UpdateHelpRequestStatusRequest request, [FromHeader(Name = "X-Moderation-Service-Key")] string? key, CancellationToken cancellationToken)
    {
        if (!Moderated(key) || !Enum.IsDefined(request.Status)) return Unauthorized(); var helpRequest = await db.HelpRequests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken); if (helpRequest is null) return NotFound(); helpRequest.Status = request.Status; helpRequest.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(cancellationToken); return Ok(helpRequest.ToResponse());
    }

    [HttpPatch("moderation/{id:guid}/comments/{commentId:guid}/visibility")]
    public async Task<IActionResult> ModerateComment(Guid id, Guid commentId, [FromHeader(Name = "X-Moderation-Service-Key")] string? key, CancellationToken cancellationToken)
    {
        if (!Moderated(key)) return Unauthorized(); var comment = await db.HelpRequestComments.SingleOrDefaultAsync(item => item.Id == commentId && item.HelpRequestId == id, cancellationToken); if (comment is null) return NotFound(); comment.IsHidden = true; await db.SaveChangesAsync(cancellationToken); return NoContent();
    }

    private bool Moderated(string? supplied) => !string.IsNullOrWhiteSpace(supplied) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(configuration["Moderation:ApiKey"] ?? "__missing__"));
    private static bool Authorize(string? code, HelpRequest request) => !string.IsNullOrWhiteSpace(code) && request.ManagementCodeHash is not null && MissingPersonSecurity.Matches(code, request.ManagementCodeHash);
    private static bool ValidCategories(HelpNeedCategory value) => value != HelpNeedCategory.None && (value & ~AllCategories) == 0;
    private static HelpRequest CreateEntity(CreateHelpRequestRequest request, Guid earthquakeId, HelpRequestSource source, HelpRequestModerationStatus moderation, string? code)
    {
        var item = new HelpRequest { Id = Guid.NewGuid(), EarthquakeId = earthquakeId, Title = request.Title.Trim(), RequesterName = request.RequesterName.Trim(), OrganizationName = request.OrganizationName?.Trim(), Address = request.Address.Trim(), Latitude = request.Latitude, Longitude = request.Longitude, NeedDetails = request.NeedDetails.Trim(), Instructions = request.Instructions?.Trim(), PublicPhone = request.PublicPhone.Trim(), PublicWhatsApp = request.PublicWhatsApp?.Trim(), PublicEmail = request.PublicEmail?.Trim(), Priority = request.Priority, NeedCategories = request.NeedCategories, NeededBy = request.NeededBy, Source = source, ModerationStatus = moderation, ManagementCodeHash = code is null ? null : MissingPersonSecurity.HashManagementCode(code) };
        item.SearchText = NormalizeSearch(string.Join(' ', item.Title, item.RequesterName, item.OrganizationName, item.Address, item.NeedDetails, item.Instructions)); return item;
    }
    private static void Apply(HelpRequest item, UpdateHelpRequestRequest request)
    {
        item.Title = request.Title.Trim(); item.RequesterName = request.RequesterName.Trim(); item.OrganizationName = request.OrganizationName?.Trim(); item.Address = request.Address.Trim(); item.Latitude = request.Latitude; item.Longitude = request.Longitude; item.NeedDetails = request.NeedDetails.Trim(); item.Instructions = request.Instructions?.Trim(); item.PublicPhone = request.PublicPhone.Trim(); item.PublicWhatsApp = request.PublicWhatsApp?.Trim(); item.PublicEmail = request.PublicEmail?.Trim(); item.Priority = request.Priority; item.NeedCategories = request.NeedCategories; item.NeededBy = request.NeededBy; item.SearchText = NormalizeSearch(string.Join(' ', item.Title, item.RequesterName, item.OrganizationName, item.Address, item.NeedDetails, item.Instructions)); item.UpdatedAt = DateTimeOffset.UtcNow;
    }
    private static string? Validate(CreateHelpRequestRequest request) => !request.PublicContactConsent ? "Public contact consent is required." : ValidateCore(request.Title, request.RequesterName, request.Address, request.NeedDetails, request.PublicPhone, request.PublicWhatsApp, request.Priority, request.NeedCategories);
    private static string? Validate(UpdateHelpRequestRequest request) => ValidateCore(request.Title, request.RequesterName, request.Address, request.NeedDetails, request.PublicPhone, request.PublicWhatsApp, request.Priority, request.NeedCategories);
    private static string? ValidateCore(string title, string requester, string address, string details, string phone, string? whatsApp, HelpRequestPriority priority, HelpNeedCategory categories)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200) return "Title is required."; if (string.IsNullOrWhiteSpace(requester) || requester.Length > 200) return "Requester name is required."; if (string.IsNullOrWhiteSpace(address) || address.Length > 400) return "Address is required."; if (string.IsNullOrWhiteSpace(details) || details.Length > 3000) return "Need details are required."; if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(whatsApp)) return "A public phone or WhatsApp number is required."; if (!Enum.IsDefined(priority)) return "Invalid priority."; if (!ValidCategories(categories)) return "At least one need category is required."; return null;
    }
    private static string NormalizeSearch(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : new(value.Normalize(NormalizationForm.FormD).Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))).Select(char.ToUpperInvariant).ToArray());
}
