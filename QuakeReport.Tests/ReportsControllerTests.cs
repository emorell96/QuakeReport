using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Controllers;
using QuakeReport.ApiService.Reports;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Validation;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Core.Models.API;
using QuakeReport.Data.Models;
using QuakeReport.Data.Geospatial;
using StorageGenerics.Core.Models;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class ReportsControllerTests
{
    [TestMethod]
    public async Task GetAllUsesNewestFirstDefaultsAndReturnsSummariesWithoutMedia()
    {
        using var db = TestDb.Create();
        var earthquakeId = QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId;
        var oldestMajor = CreateReport(earthquakeId, SeverityLevel.Major, new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero));
        var newestMajor = CreateReport(earthquakeId, SeverityLevel.Major, new DateTimeOffset(2026, 8, 10, 11, 0, 0, TimeSpan.Zero));
        var catastrophic = CreateReport(earthquakeId, SeverityLevel.Catastrophic, new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero));
        catastrophic.Media.Add(new ReportMedia
        {
            Id = Guid.NewGuid(),
            DamageReportId = catastrophic.Id,
            BlobUrl = "https://storage.test/video.mp4",
            MediaType = MediaType.Video,
            FileName = "video.mp4",
            ContentType = "video/mp4",
            SizeBytes = 100,
        });
        db.DamageReports.AddRange(oldestMajor, newestMajor, catastrophic);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.GetAll(new PaginationRequest(), CancellationToken.None);

        var ok = TestAssert.InstanceOf<OkObjectResult>(result);
        var response = TestAssert.InstanceOf<PagedResult<DamageReportSummaryResponse>>(ok.Value);
        CollectionAssert.AreEqual(
            new[] { newestMajor.Id, oldestMajor.Id, catastrophic.Id },
            response.Results.Select(report => report.Id).ToArray());
        Assert.AreEqual(1, response.PageNumber);
        Assert.AreEqual(20, response.PageSize);
        Assert.AreEqual(3, response.TotalMatches);
        Assert.AreEqual(1, response.TotalPages);
        Assert.IsNull(typeof(DamageReportSummaryResponse).GetProperty("Media"));
    }

    [TestMethod]
    public async Task GetAllReturnsEmptySuccessfulResponseWhenNoReportsExist()
    {
        using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await controller.GetAll(new PaginationRequest(), CancellationToken.None);

        var ok = TestAssert.InstanceOf<OkObjectResult>(result);
        var response = TestAssert.InstanceOf<PagedResult<DamageReportSummaryResponse>>(ok.Value);
        Assert.AreEqual(0, response.Results.Count);
        Assert.AreEqual(0, response.TotalMatches);
        Assert.AreEqual(0, response.TotalPages);
    }

    [TestMethod]
    public async Task GetAllReturnsUnprocessableEntityWhenNoEarthquakeIsActive()
    {
        using var db = TestDb.Create();
        db.Earthquakes.RemoveRange(db.Earthquakes);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.GetAll(new PaginationRequest(), CancellationToken.None);

        TestAssert.InstanceOf<UnprocessableEntityObjectResult>(result);
    }

    [TestMethod]
    public async Task GetAllAppliesDefaultPageSizeAndReturnsTheNextPage()
    {
        using var db = TestDb.Create();
        var earthquakeId = QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId;
        var start = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);
        var reports = Enumerable.Range(0, 21)
            .Select(index => CreateReport(earthquakeId, SeverityLevel.Moderate, start.AddMinutes(index)))
            .ToList();
        db.DamageReports.AddRange(reports);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var firstResult = await controller.GetAll(new PaginationRequest(), CancellationToken.None);
        var firstPage = TestAssert.InstanceOf<PagedResult<DamageReportSummaryResponse>>(
            TestAssert.InstanceOf<OkObjectResult>(firstResult).Value);
        var secondResult = await controller.GetAll(
            new PaginationRequest { Page = 2 },
            CancellationToken.None);
        var secondPage = TestAssert.InstanceOf<PagedResult<DamageReportSummaryResponse>>(
            TestAssert.InstanceOf<OkObjectResult>(secondResult).Value);

        Assert.AreEqual(20, firstPage.Results.Count);
        Assert.AreEqual(21, firstPage.TotalMatches);
        Assert.AreEqual(2, firstPage.TotalPages);
        Assert.AreEqual(1, secondPage.Results.Count);
        Assert.AreEqual(reports[0].Id, secondPage.Results.Single().Id);
    }

    [TestMethod]
    public async Task SearchFiltersBeforePaginatingAndReturnsTotalMetadata()
    {
        using var db = TestDb.Create();
        var earthquakeId = QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId;
        var oldestMajor = CreateReport(earthquakeId, SeverityLevel.Major, new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero));
        var newestMajor = CreateReport(earthquakeId, SeverityLevel.Major, new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
        var severe = CreateReport(earthquakeId, SeverityLevel.Severe, new DateTimeOffset(2026, 8, 10, 11, 0, 0, TimeSpan.Zero));
        db.DamageReports.AddRange(oldestMajor, newestMajor, severe);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await SearchAsync(
            controller,
            page: 2,
            pageSize: 1,
            severity: SeverityLevel.Major);

        var ok = TestAssert.InstanceOf<OkObjectResult>(result);
        var response = TestAssert.InstanceOf<PagedResult<DamageReportSummaryResponse>>(ok.Value);
        Assert.AreEqual(2, response.PageNumber);
        Assert.AreEqual(1, response.PageSize);
        Assert.AreEqual(2, response.TotalMatches);
        Assert.AreEqual(2, response.TotalPages);
        Assert.AreEqual(1, response.Results.Count);
        Assert.AreEqual(oldestMajor.Id, response.Results.Single().Id);
    }

    [TestMethod]
    public async Task SearchUsesAnExplicitEarthquakeInsteadOfTheActiveEarthquake()
    {
        using var db = TestDb.Create();
        var selectedEarthquakeId = Guid.NewGuid();
        var activeReport = CreateReport(
            QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId,
            SeverityLevel.Major,
            DateTimeOffset.UtcNow);
        var selectedReport = CreateReport(
            selectedEarthquakeId,
            SeverityLevel.Major,
            DateTimeOffset.UtcNow);
        db.DamageReports.AddRange(activeReport, selectedReport);
        await db.SaveChangesAsync();
        var controller = CreateController(db);
        var request = new PagedRequest<DamageReportSearchFilter>
        {
            Filter = new DamageReportSearchFilter
            {
                EarthquakeId = selectedEarthquakeId,
            },
        };

        var result = await controller.Search(request, CancellationToken.None);
        var response = TestAssert.InstanceOf<PagedResult<DamageReportSummaryResponse>>(
            TestAssert.InstanceOf<OkObjectResult>(result).Value);

        Assert.AreEqual(1, response.TotalMatches);
        Assert.AreEqual(selectedReport.Id, response.Results.Single().Id);
    }

    [TestMethod]
    public async Task SearchSupportsEverySortOption()
    {
        using var db = TestDb.Create();
        var earthquakeId = QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId;
        var oldestMajor = CreateReport(earthquakeId, SeverityLevel.Major, new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero));
        var newestMinor = CreateReport(earthquakeId, SeverityLevel.Minor, new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
        var severe = CreateReport(earthquakeId, SeverityLevel.Severe, new DateTimeOffset(2026, 8, 10, 11, 0, 0, TimeSpan.Zero));
        db.DamageReports.AddRange(oldestMajor, newestMinor, severe);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var expectations = new Dictionary<ReportSortOption, Guid[]>
        {
            [ReportSortOption.Newest] = [newestMinor.Id, severe.Id, oldestMajor.Id],
            [ReportSortOption.Oldest] = [oldestMajor.Id, severe.Id, newestMinor.Id],
            [ReportSortOption.HighestSeverity] = [severe.Id, oldestMajor.Id, newestMinor.Id],
            [ReportSortOption.LowestSeverity] = [newestMinor.Id, oldestMajor.Id, severe.Id],
        };

        foreach (var (sort, expectedIds) in expectations)
        {
            var result = await SearchAsync(controller, sort: sort);
            var ok = TestAssert.InstanceOf<OkObjectResult>(result);
            var response = TestAssert.InstanceOf<PagedResult<DamageReportSummaryResponse>>(ok.Value);
            CollectionAssert.AreEqual(expectedIds, response.Results.Select(report => report.Id).ToArray(), sort.ToString());
        }
    }

    [TestMethod]
    public async Task SearchUsesReportIdAsDeterministicTieBreaker()
    {
        using var db = TestDb.Create();
        var timestamp = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var lowerId = CreateReport(QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId, SeverityLevel.Major, timestamp);
        lowerId.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherId = CreateReport(QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId, SeverityLevel.Major, timestamp);
        higherId.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        db.DamageReports.AddRange(lowerId, higherId);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var newestResult = await SearchAsync(controller, sort: ReportSortOption.Newest);
        var newest = TestAssert.InstanceOf<PagedResult<DamageReportSummaryResponse>>(
            TestAssert.InstanceOf<OkObjectResult>(newestResult).Value);
        var oldestResult = await SearchAsync(controller, sort: ReportSortOption.Oldest);
        var oldest = TestAssert.InstanceOf<PagedResult<DamageReportSummaryResponse>>(
            TestAssert.InstanceOf<OkObjectResult>(oldestResult).Value);

        CollectionAssert.AreEqual(new[] { higherId.Id, lowerId.Id }, newest.Results.Select(report => report.Id).ToArray());
        CollectionAssert.AreEqual(new[] { lowerId.Id, higherId.Id }, oldest.Results.Select(report => report.Id).ToArray());
    }

    [TestMethod]
    [DataRow(0, 20, SeverityLevel.Minor, ReportSortOption.Newest)]
    [DataRow(1, 0, SeverityLevel.Minor, ReportSortOption.Newest)]
    [DataRow(1, 101, SeverityLevel.Minor, ReportSortOption.Newest)]
    [DataRow(1, 20, (SeverityLevel)999, ReportSortOption.Newest)]
    [DataRow(1, 20, SeverityLevel.Minor, (ReportSortOption)999)]
    public async Task SearchRejectsInvalidValues(
        int page,
        int pageSize,
        SeverityLevel severity,
        ReportSortOption sort)
    {
        using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await SearchAsync(controller, page, pageSize, severity, sort);

        var badRequest = TestAssert.InstanceOf<BadRequestObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        var details = TestAssert.InstanceOf<ValidationProblemDetails>(badRequest.Value);
        Assert.AreEqual(StatusCodes.Status400BadRequest, details.Status);
    }

    [TestMethod]
    public async Task GetByIdReturnsMappedReport()
    {
        using var db = TestDb.Create();
        var report = CreateReport(
            QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId,
            SeverityLevel.Severe,
            DateTimeOffset.UtcNow,
            StructureType.Commercial,
            StructureSize.Medium);
        report.Media.Add(new ReportMedia
        {
            Id = Guid.NewGuid(),
            DamageReportId = report.Id,
            BlobUrl = "https://storage.test/photo.jpg",
            MediaType = MediaType.Photo,
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 10,
        });
        db.DamageReports.Add(report);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.GetById(report.Id, CancellationToken.None);

        var ok = TestAssert.InstanceOf<OkObjectResult>(result);
        var response = TestAssert.InstanceOf<DamageReportResponse>(ok.Value);
        Assert.AreEqual(report.Id, response.Id);
        Assert.AreEqual(StructureType.Commercial, response.StructureType);
        Assert.AreEqual(StructureSize.Medium, response.StructureSize);
        Assert.AreEqual(1, response.Media.Count);
        Assert.AreEqual("https://storage.test/photo.jpg", response.Media[0].BlobUrl);
    }

    [TestMethod]
    public async Task GetByIdReturnsNotFoundForUnknownReport()
    {
        using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        TestAssert.InstanceOf<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task CreateReturnsUnprocessableEntityWithoutActiveEarthquake()
    {
        using var db = TestDb.Create();
        db.Earthquakes.RemoveRange(db.Earthquakes);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Create(CreateRequest(), CancellationToken.None);

        var unprocessable = TestAssert.InstanceOf<UnprocessableEntityObjectResult>(result);
        Assert.AreEqual(0, db.DamageReports.Count());
        StringAssert.Contains(unprocessable.Value?.ToString(), "No active earthquake");
    }

    [TestMethod]
    public async Task CreateCopiesAllFieldsIncludingStructureFieldsAndPersistsReport()
    {
        using var db = TestDb.Create();
        var request = CreateRequest(StructureType.ApartmentComplex, StructureSize.Large);
        var controller = CreateController(db);

        var result = await controller.Create(request, CancellationToken.None);

        var created = TestAssert.InstanceOf<CreatedAtActionResult>(result);
        var response = TestAssert.InstanceOf<DamageReportResponse>(created.Value);
        var entity = await db.DamageReports.SingleAsync(report => report.Id == response.Id);
        Assert.AreEqual(QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId, entity.EarthquakeId);
        Assert.AreEqual(request.Description, entity.Description);
        Assert.AreEqual(request.Severity, entity.Severity);
        Assert.AreEqual(request.DamageSigns, entity.DamageSigns);
        Assert.AreEqual(request.StructureType, entity.StructureType);
        Assert.AreEqual(request.StructureSize, entity.StructureSize);
        Assert.AreEqual(request.Latitude, entity.Location.Y);
        Assert.AreEqual(request.Longitude, entity.Location.X);
        Assert.AreEqual(request.Address, entity.Address);
        Assert.AreEqual(response.Id, created.RouteValues!["id"]);
    }

    [TestMethod]
    public async Task CreatePreservesNullStructureFields()
    {
        using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await controller.Create(CreateRequest(), CancellationToken.None);

        var created = TestAssert.InstanceOf<CreatedAtActionResult>(result);
        var response = TestAssert.InstanceOf<DamageReportResponse>(created.Value);
        Assert.IsNull(response.StructureType);
        Assert.IsNull(response.StructureSize);
    }

    [TestMethod]
    public async Task UploadMediaReturnsNotFoundForUnknownReport()
    {
        using var db = TestDb.Create();
        var storage = new RecordingMediaStorage();
        var controller = CreateController(db, storage);

        var result = await controller.UploadMedia(Guid.NewGuid(), CreateUploadRequest(), CancellationToken.None);

        TestAssert.InstanceOf<NotFoundResult>(result);
        Assert.AreEqual(0, storage.CallCount);
    }

    [TestMethod]
    public async Task UploadMediaRejectsEmptyFilesWithoutCallingStorage()
    {
        using var db = TestDb.Create();
        var report = CreateReport(QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId, SeverityLevel.Minor, DateTimeOffset.UtcNow);
        db.DamageReports.Add(report);
        await db.SaveChangesAsync();
        var storage = new RecordingMediaStorage();
        var controller = CreateController(db, storage);

        var result = await controller.UploadMedia(report.Id, CreateUploadRequest(length: 0), CancellationToken.None);

        var badRequest = TestAssert.InstanceOf<BadRequestObjectResult>(result);
        Assert.AreEqual("File is empty.", badRequest.Value);
        Assert.AreEqual(0, storage.CallCount);
    }

    [TestMethod]
    public async Task UploadMediaRejectsFilesLargerThan50Mb()
    {
        using var db = TestDb.Create();
        var report = CreateReport(QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId, SeverityLevel.Minor, DateTimeOffset.UtcNow);
        db.DamageReports.Add(report);
        await db.SaveChangesAsync();
        var storage = new RecordingMediaStorage();
        var controller = CreateController(db, storage);

        var result = await controller.UploadMedia(report.Id, CreateUploadRequest(length: 50 * 1024 * 1024 + 1), CancellationToken.None);

        var badRequest = TestAssert.InstanceOf<BadRequestObjectResult>(result);
        StringAssert.Contains(badRequest.Value?.ToString(), "50 MB limit");
        Assert.AreEqual(0, storage.CallCount);
    }

    [TestMethod]
    public async Task UploadMediaAcceptsExactly50MbAndCaseInsensitiveImageType()
    {
        using var db = TestDb.Create();
        var report = CreateReport(QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId, SeverityLevel.Minor, DateTimeOffset.UtcNow);
        db.DamageReports.Add(report);
        await db.SaveChangesAsync();
        var storage = new RecordingMediaStorage();
        var controller = CreateController(db, storage);

        var result = await controller.UploadMedia(
            report.Id,
            CreateUploadRequest(length: 50 * 1024 * 1024, contentType: "IMAGE/JPEG", content: [1, 2, 3]),
            CancellationToken.None);

        var created = TestAssert.InstanceOf<CreatedAtActionResult>(result);
        var response = TestAssert.InstanceOf<ReportMediaResponse>(created.Value);
        var media = await db.ReportMedia.SingleAsync();
        Assert.AreEqual(50 * 1024 * 1024, media.SizeBytes);
        Assert.AreEqual("IMAGE/JPEG", storage.ContentType);
        Assert.AreEqual(response.BlobUrl, media.BlobUrl);
    }

    [TestMethod]
    public async Task UploadMediaRejectsNonImageAndNonVideoContentTypes()
    {
        using var db = TestDb.Create();
        var report = CreateReport(QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId, SeverityLevel.Minor, DateTimeOffset.UtcNow);
        db.DamageReports.Add(report);
        await db.SaveChangesAsync();
        var storage = new RecordingMediaStorage();
        var controller = CreateController(db, storage);

        var result = await controller.UploadMedia(
            report.Id,
            CreateUploadRequest(contentType: "application/pdf", content: [1]),
            CancellationToken.None);

        var badRequest = TestAssert.InstanceOf<BadRequestObjectResult>(result);
        Assert.AreEqual("Only image and video uploads are allowed.", badRequest.Value);
        Assert.AreEqual(0, storage.CallCount);
    }

    [TestMethod]
    public async Task UploadMediaPassesMetadataPersistsMediaAndReturnsUrl()
    {
        using var db = TestDb.Create();
        var report = CreateReport(QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId, SeverityLevel.Minor, DateTimeOffset.UtcNow);
        db.DamageReports.Add(report);
        await db.SaveChangesAsync();
        var storage = new RecordingMediaStorage();
        var controller = CreateController(db, storage);

        var result = await controller.UploadMedia(
            report.Id,
            CreateUploadRequest(fileName: "evidence.mp4", contentType: "video/mp4", mediaType: MediaType.Video, content: [4, 5, 6]),
            CancellationToken.None);

        var created = TestAssert.InstanceOf<CreatedAtActionResult>(result);
        var response = TestAssert.InstanceOf<ReportMediaResponse>(created.Value);
        var media = await db.ReportMedia.SingleAsync();
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, storage.UploadedContent);
        Assert.AreEqual(report.Id, storage.ReportId);
        Assert.AreEqual("evidence.mp4", storage.FileName);
        Assert.AreEqual("video/mp4", storage.ContentType);
        Assert.AreEqual(MediaType.Video, media.MediaType);
        Assert.AreEqual("evidence.mp4", media.FileName);
        Assert.AreEqual("video/mp4", media.ContentType);
        Assert.AreEqual(3, media.SizeBytes);
        Assert.AreEqual(storage.Url, response.BlobUrl);
        Assert.AreEqual(report.Id, created.RouteValues!["id"]);
    }

    [TestMethod]
    public async Task UploadMediaDoesNotPersistWhenStorageFails()
    {
        using var db = TestDb.Create();
        var report = CreateReport(QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId, SeverityLevel.Minor, DateTimeOffset.UtcNow);
        db.DamageReports.Add(report);
        await db.SaveChangesAsync();
        var storage = new RecordingMediaStorage { ExceptionToThrow = new InvalidOperationException("storage unavailable") };
        var controller = CreateController(db, storage);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            controller.UploadMedia(report.Id, CreateUploadRequest(content: [1]), CancellationToken.None));

        Assert.AreEqual(0, db.ReportMedia.Count());
    }

    private static ReportsController CreateController(QuakeReport.Data.QuakeReportDbContext db, RecordingMediaStorage? storage = null) =>
        new(
            new DamageReportService(db, TestRepository.Create<DamageReport>(db)),
            new ActiveEarthquakeService(db),
            storage ?? new RecordingMediaStorage(),
            new PaginationRequestValidator(),
            new DamageReportSearchRequestValidator(new DamageReportSearchFilterValidator()));

    private static Task<ActionResult<PagedResult<DamageReportSummaryResponse>>> SearchAsync(
        ReportsController controller,
        int page = 1,
        int pageSize = 20,
        SeverityLevel? severity = null,
        ReportSortOption sort = ReportSortOption.Newest)
    {
        var request = new PagedRequest<DamageReportSearchFilter>
        {
            PageNumber = page,
            PageSize = pageSize,
            Filter = new DamageReportSearchFilter
            {
                EarthquakeId = null,
                Severity = severity,
                Sort = sort,
            },
        };

        return controller.Search(request, CancellationToken.None);
    }

    private static CreateDamageReportRequest CreateRequest(
        StructureType? structureType = null,
        StructureSize? structureSize = null) =>
        new()
        {
            Description = "Damaged apartment",
            Severity = SeverityLevel.Severe,
            DamageSigns = DamageSign.Cracks | DamageSign.FallenDebris,
            StructureType = structureType,
            StructureSize = structureSize,
            Latitude = 4.5709,
            Longitude = -74.2973,
            Address = "Main Street",
            PrivacyConsent = true,
        };

    private static UploadReportMediaRequest CreateUploadRequest(
        long length = 3,
        string fileName = "evidence.jpg",
        string contentType = "image/jpeg",
        MediaType mediaType = MediaType.Photo,
        byte[]? content = null) =>
        new()
        {
            File = new TestFormFile(length, fileName, contentType, content ?? [1, 2, 3]),
            MediaType = mediaType,
        };

    private static DamageReport CreateReport(
        Guid earthquakeId,
        SeverityLevel severity,
        DateTimeOffset createdAt,
        StructureType? structureType = null,
        StructureSize? structureSize = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            EarthquakeId = earthquakeId,
            Description = "Report",
            Severity = severity,
            DamageSigns = DamageSign.None,
            StructureType = structureType,
            StructureSize = structureSize,
            Location = GeoPoint.FromCoordinates(4, -74),
            CreatedAt = createdAt,
        };
}
