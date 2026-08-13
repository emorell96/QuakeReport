using Microsoft.AspNetCore.Mvc;
using QuakeReport.ApiService.GeocodingReview;
using QuakeReport.ApiService.Security;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data.Geospatial;
using QuakeReport.Geospatial;

namespace QuakeReport.ApiService.Controllers;

[ApiController]
[Route("api/geocoding-review")]
public sealed class GeocodingReviewController(
    IGeocodingReviewService reviews,
    GeocodingCoordinator coordinator,
    IModerationKeyValidator moderationKey) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        [FromQuery] GeocodingReviewStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }
        var items = await reviews.GetLatestAsync(status, 500, cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }
        return await coordinator.RetryAsync(id, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPut("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, ResolveGeocodingReviewRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }
        if (!GeoPoint.IsValid(request.Latitude, request.Longitude))
        {
            return BadRequest("Coordenadas inválidas.");
        }
        var review = await reviews.GetForUpdateAsync(id, cancellationToken);
        if (review is null)
        {
            return NotFound();
        }
        var entity = await coordinator.FindEntityAsync(review.EntityType, review.EntityId, cancellationToken);
        if (entity is null)
        {
            return NotFound("La entidad ya no existe.");
        }
        entity.Location = GeoPoint.FromCoordinates(request.Latitude, request.Longitude);
        Complete(review, GeocodingReviewStatus.Resolved, request.ResolvedBy);
        await reviews.PersistUpdateAsync(review, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, DismissGeocodingReviewRequest request,
        [FromHeader(Name = "X-Moderation-Service-Key")] string? key,
        CancellationToken cancellationToken)
    {
        if (!moderationKey.IsValid(key))
        {
            return Unauthorized();
        }
        var review = await reviews.GetForUpdateAsync(id, cancellationToken);
        if (review is null)
        {
            return NotFound();
        }
        Complete(review, GeocodingReviewStatus.Dismissed, request.ResolvedBy);
        await reviews.PersistUpdateAsync(review, cancellationToken);
        return NoContent();
    }

    private static void Complete(Data.Models.GeocodingReviewItem item, GeocodingReviewStatus status, string? resolvedBy)
    {
        item.Status = status;
        item.ResolvedAt = DateTimeOffset.UtcNow;
        item.ResolvedBy = resolvedBy?.Trim();
    }

    private static GeocodingReviewItemResponse ToResponse(Data.Models.GeocodingReviewItem item) => new(
        item.Id, item.EntityType, item.EntityId, item.AddressSnapshot, item.Status, item.Reason,
        GeoPoint.Latitude(item.CandidateLocation), GeoPoint.Longitude(item.CandidateLocation),
        item.FormattedAddress, item.GooglePlaceId, item.Granularity, item.AttemptCount, item.LastAttemptAt);
}
