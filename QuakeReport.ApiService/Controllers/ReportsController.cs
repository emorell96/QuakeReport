using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Media;
using QuakeReport.ApiService.Pagination;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;
using QuakeReport.Data.Geospatial;
using StorageGenerics.Core.Contracts;
using StorageGenerics.Core.Models;
using StorageGenerics.Extensions;

namespace QuakeReport.ApiService.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(
    QuakeReportDbContext dbContext,
    ActiveEarthquakeService activeEarthquakeService,
    IMediaStorage mediaStorage,
    IQueryableRepositoryService<DamageReport, Guid> reportsRepository) : ControllerBase
{
    private const long MaxMediaSizeBytes = 50 * 1024 * 1024; // 50 MB

    /// <summary>Returns a filtered, sorted page of report summaries.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<DamageReportSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationParameters.DefaultPageSize,
        [FromQuery] SeverityLevel? severity = null,
        [FromQuery] ReportSortOption sort = ReportSortOption.Newest,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "Page must be at least 1.");
        }

        if (pageSize is < 1 or > PaginationParameters.MaxPageSize)
        {
            ModelState.AddModelError(nameof(pageSize), $"Page size must be between 1 and {PaginationParameters.MaxPageSize}.");
        }

        if (severity.HasValue && !Enum.IsDefined(severity.Value))
        {
            ModelState.AddModelError(nameof(severity), "Severity is invalid.");
        }

        if (!Enum.IsDefined(sort))
        {
            ModelState.AddModelError(nameof(sort), "Sort option is invalid.");
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more report query parameters are invalid.",
            });
        }

        var query = reportsRepository.QueryAll().AsNoTracking();
        if (severity.HasValue)
        {
            query = query.Where(report => report.Severity == severity.Value);
        }

        var orderedQuery = sort switch
        {
            ReportSortOption.Oldest => query
                .OrderBy(report => report.CreatedAt)
                .ThenBy(report => report.Id),
            ReportSortOption.HighestSeverity => query
                .OrderByDescending(report => report.Severity)
                .ThenByDescending(report => report.CreatedAt)
                .ThenByDescending(report => report.Id),
            ReportSortOption.LowestSeverity => query
                .OrderBy(report => report.Severity)
                .ThenByDescending(report => report.CreatedAt)
                .ThenByDescending(report => report.Id),
            _ => query
                .OrderByDescending(report => report.CreatedAt)
                .ThenByDescending(report => report.Id),
        };

        var projected = orderedQuery.SelectOrdered(report => report.ToSummaryResponse());
        return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DamageReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var report = await dbContext.DamageReports
            .Include(r => r.Media)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (report is null)
        {
            return NotFound();
        }

        return Ok(report.ToResponse());
    }

    [HttpPost]
    [ProducesResponseType<DamageReportResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateDamageReportRequest request, CancellationToken cancellationToken)
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

        dbContext.DamageReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = report.Id }, report.ToResponse());
    }

    [HttpPost("{id:guid}/media")]
    [ProducesResponseType<ReportMediaResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(MaxMediaSizeBytes)]
    public async Task<IActionResult> UploadMedia(Guid id, [FromForm] UploadReportMediaRequest request, CancellationToken cancellationToken)
    {
        var reportExists = await dbContext.DamageReports.AnyAsync(r => r.Id == id, cancellationToken);
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

        dbContext.ReportMedia.Add(media);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, media.ToResponse());
    }
}
