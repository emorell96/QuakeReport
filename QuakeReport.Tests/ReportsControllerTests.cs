using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Controllers;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data.Models;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class ReportsControllerTests
{
    [TestMethod]
    public async Task GetAllOrdersBySeverityThenCreatedAtAndIncludesMedia()
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

        var result = await controller.GetAll(CancellationToken.None);

        var ok = TestAssert.InstanceOf<OkObjectResult>(result);
        var reports = TestAssert.InstanceOf<IEnumerable<DamageReportResponse>>(ok.Value).ToList();
        CollectionAssert.AreEqual(
            new[] { catastrophic.Id, newestMajor.Id, oldestMajor.Id },
            reports.Select(report => report.Id).ToArray());
        Assert.AreEqual(1, reports[0].Media.Count);
        Assert.AreEqual("https://storage.test/video.mp4", reports[0].Media[0].BlobUrl);
    }

    [TestMethod]
    public async Task GetAllReturnsEmptySuccessfulResponseWhenNoReportsExist()
    {
        using var db = TestDb.Create();
        var controller = CreateController(db);

        var result = await controller.GetAll(CancellationToken.None);

        var ok = TestAssert.InstanceOf<OkObjectResult>(result);
        var reports = TestAssert.InstanceOf<IEnumerable<DamageReportResponse>>(ok.Value);
        Assert.AreEqual(0, reports.Count());
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
        db.DamageReports.Add(report);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.GetById(report.Id, CancellationToken.None);

        var ok = TestAssert.InstanceOf<OkObjectResult>(result);
        var response = TestAssert.InstanceOf<DamageReportResponse>(ok.Value);
        Assert.AreEqual(report.Id, response.Id);
        Assert.AreEqual(StructureType.Commercial, response.StructureType);
        Assert.AreEqual(StructureSize.Medium, response.StructureSize);
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
        Assert.AreEqual(request.Latitude, entity.Latitude);
        Assert.AreEqual(request.Longitude, entity.Longitude);
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
        new(db, new ActiveEarthquakeService(db), storage ?? new RecordingMediaStorage());

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
            Latitude = 4,
            Longitude = -74,
            CreatedAt = createdAt,
        };
}
