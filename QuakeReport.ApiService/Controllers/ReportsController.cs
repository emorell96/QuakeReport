using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Media;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.ApiService.Pagination;
using QuakeReport.ApiService.Reports;
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
[Route("api/reports")]
public class ReportsController(
    IDamageReportService reports,
    IActiveEarthquakeService activeEarthquakeService,
    IMediaStorage mediaStorage,
    IValidator<PaginationRequest> paginationValidator,
    IValidator<PagedRequest<DamageReportSearchFilter>> searchValidator) : ControllerBase
{
    private const long MaxMediaSizeBytes = 50 * 1024 * 1024; // 50 MB

    /// <summary>Returns a page of report summaries for the active earthquake.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<DamageReportSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<DamageReportSummaryResponse>>> GetAll(
        [FromQuery] PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        var validation = await paginationValidator.ValidateAsync(pagination, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToProblemDetails("Invalid pagination parameters."));
        }

        var earthquakeId = await activeEarthquakeService.ResolveEarthquakeIdAsync(
            null,
            cancellationToken);
        if (earthquakeId is null)
        {
            return UnprocessableEntity("No active earthquake is configured.");
        }

        var criteria = new DamageReportQueryCriteria(
            earthquakeId.Value,
            null,
            ReportSortOption.Newest);
        var orderedQuery = reports.GetOrderedQuery(criteria);
        var projected = orderedQuery.SelectOrdered(report => report.ToSummaryResponse());
        var result = await projected.ToPagedResultAsync(
            pagination.Page,
            pagination.PageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("search")]
    [ProducesResponseType<PagedResult<DamageReportSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResult<DamageReportSummaryResponse>>> Search(
        [FromBody] PagedRequest<DamageReportSearchFilter> request,
        CancellationToken cancellationToken = default)
    {
        var validation = await searchValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ToProblemDetails("Invalid damage-report search."));
        }

        var filter = request.Filter!;
        var earthquakeId = await activeEarthquakeService.ResolveEarthquakeIdAsync(
            filter.EarthquakeId,
            cancellationToken);
        if (earthquakeId is null)
        {
            return UnprocessableEntity("No active earthquake is configured.");
        }

        var criteria = new DamageReportQueryCriteria(
            earthquakeId.Value,
            filter.Severity,
            filter.Sort);
        var orderedQuery = reports.GetOrderedQuery(criteria);
        var projected = orderedQuery.SelectOrdered(report => report.ToSummaryResponse());
        var result = await projected.ToPagedResultAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DamageReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DamageReportResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(id, cancellationToken);

        if (report is null)
        {
            return NotFound();
        }

        return Ok(report.ToResponse());
    }

    [HttpPost]
    [ProducesResponseType<DamageReportResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DamageReportResponse>> Create(CreateDamageReportRequest request, CancellationToken cancellationToken)
    {
        if (!request.PrivacyConsent)
        {
            return BadRequest("Privacy consent is required.");
        }

        var activeEarthquake = await activeEarthquakeService.GetActiveEarthquakeAsync(cancellationToken);
        if (activeEarthquake is null)
        {
            return UnprocessableEntity("No active earthquake is configured to attribute this report to.");
        }

        var report = new DamageReport
        {
            Id = Guid.NewGuid(),
            EarthquakeId = activeEarthquake.Id,
            Description = request.Description,
            Severity = request.Severity,
            DamageSigns = request.DamageSigns,
            StructureType = request.StructureType,
            StructureSize = request.StructureSize,
            Location = GeoPoint.FromCoordinates(request.Latitude, request.Longitude),
            Address = request.Address,
        };

        await reports.CreateAsync(report, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = report.Id }, report.ToResponse());
    }

    [HttpPost("{id:guid}/media")]
    [ProducesResponseType<ReportMediaResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(MaxMediaSizeBytes)]
    public async Task<ActionResult<ReportMediaResponse>> UploadMedia(Guid id, [FromForm] UploadReportMediaRequest request, CancellationToken cancellationToken)
    {
        var reportExists = await reports.ExistsAsync(id, cancellationToken);
        if (!reportExists)
        {
            return NotFound();
        }

        var file = request.File;

        if (file.Length == 0)
        {
            return BadRequest("File is empty.");
        }

        if (file.Length > MaxMediaSizeBytes)
        {
            return BadRequest($"File exceeds the {MaxMediaSizeBytes / (1024 * 1024)} MB limit.");
        }

        var isAllowedContentType = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
        if (!isAllowedContentType)
        {
            return BadRequest("Only image and video uploads are allowed.");
        }

        var media = new ReportMedia
        {
            Id = Guid.NewGuid(),
            DamageReportId = id,
            MediaType = request.MediaType,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            BlobUrl = string.Empty,
        };

        await using (var stream = file.OpenReadStream())
        {
            media.BlobUrl = await mediaStorage.UploadAsync(
                id, media.Id, file.FileName, file.ContentType, stream, cancellationToken);
        }

        await reports.AttachMediaAsync(media, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, media.ToResponse());
    }
}
