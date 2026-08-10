using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Dtos;
using QuakeReport.Data.Models;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class DtoAndDataTests
{
    [TestMethod]
    public void DamageReportResponseMapsStructureFieldsAndMedia()
    {
        var reportId = Guid.NewGuid();
        var earthquakeId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 8, 10, 12, 30, 0, TimeSpan.Zero);
        var uploadedAt = createdAt.AddMinutes(1);
        var media = new ReportMedia
        {
            Id = Guid.NewGuid(),
            DamageReportId = reportId,
            BlobUrl = "https://storage.test/photo.jpg",
            MediaType = MediaType.Photo,
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 12,
            UploadedAt = uploadedAt,
        };
        var report = new DamageReport
        {
            Id = reportId,
            EarthquakeId = earthquakeId,
            Description = "Cracked wall",
            Severity = SeverityLevel.Major,
            DamageSigns = DamageSign.Cracks | DamageSign.GasSmell,
            StructureType = StructureType.Apartment,
            StructureSize = StructureSize.Large,
            Latitude = 4.5,
            Longitude = -74.3,
            Address = "Main Street",
            CreatedAt = createdAt,
            Media = [media],
        };

        var result = DamageReportResponse.FromEntity(report);

        Assert.AreEqual(reportId, result.Id);
        Assert.AreEqual(earthquakeId, result.EarthquakeId);
        Assert.AreEqual(report.Description, result.Description);
        Assert.AreEqual(report.Severity, result.Severity);
        Assert.AreEqual(report.DamageSigns, result.DamageSigns);
        Assert.AreEqual(StructureType.Apartment, result.StructureType);
        Assert.AreEqual(StructureSize.Large, result.StructureSize);
        Assert.AreEqual(report.Latitude, result.Latitude);
        Assert.AreEqual(report.Longitude, result.Longitude);
        Assert.AreEqual(report.Address, result.Address);
        Assert.AreEqual(createdAt, result.CreatedAt);
        Assert.AreEqual(1, result.Media.Count);
        Assert.AreEqual(media.BlobUrl, result.Media[0].BlobUrl);
        Assert.AreEqual(media.MediaType, result.Media[0].MediaType);
        Assert.AreEqual(uploadedAt, result.Media[0].UploadedAt);
    }

    [TestMethod]
    public void DamageReportResponsePreservesNullOptionalStructureFields()
    {
        var report = new DamageReport
        {
            Id = Guid.NewGuid(),
            EarthquakeId = Guid.NewGuid(),
            Description = "Blocked road",
            Severity = SeverityLevel.Minor,
            Latitude = 1,
            Longitude = 2,
        };

        var result = DamageReportResponse.FromEntity(report);

        Assert.IsNull(result.StructureType);
        Assert.IsNull(result.StructureSize);
        Assert.AreEqual(0, result.Media.Count);
    }

    [TestMethod]
    public void DamageReportModelConfiguresNullableStructureFieldsAndRelationships()
    {
        using var db = TestDb.Create();
        var entity = db.Model.FindEntityType(typeof(DamageReport));

        Assert.IsNotNull(entity);
        Assert.IsTrue(entity!.FindProperty(nameof(DamageReport.StructureType))!.IsNullable);
        Assert.IsTrue(entity.FindProperty(nameof(DamageReport.StructureSize))!.IsNullable);
        Assert.IsNotNull(entity.FindNavigation(nameof(DamageReport.Media)));
        Assert.IsTrue(entity.GetForeignKeys().Any(fk => fk.Properties.Any(property => property.Name == nameof(DamageReport.EarthquakeId))));

        var seeded = db.Earthquakes.Single(e => e.Id == QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId);
        Assert.IsTrue(seeded.IsActive);
        Assert.AreEqual("M7.4 - Colombia", seeded.Name);
    }
}
