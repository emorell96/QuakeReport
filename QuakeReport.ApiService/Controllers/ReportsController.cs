using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Media;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Data;
using QuakeReport.Data.Models;

namespace QuakeReport.ApiService.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(
    QuakeReportDbContext dbContext,
    ActiveEarthquakeService activeEarthquakeService,
    IMediaStorage mediaStorage) : ControllerBase
{
    private const long MaxMediaSizeBytes = 50 * 1024 * 1024; // 50 MB

    /// <summary>Worst-impact-first, newest-first within the same severity.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DamageReportResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var reports = await dbContext.DamageReports
            .Include(r => r.Media)
            .OrderByDescending(r => r.Severity)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(reports.Select(r => r.ToResponse()));
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
            Latitude = request.Latitude,
            Longitude = request.Longitude,
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
