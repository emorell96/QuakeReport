using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data.Models;
using QuakeReport.Data.Geospatial;

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
            Location = GeoPoint.FromCoordinates(4.5, -74.3),
            Address = "Main Street",
            CreatedAt = createdAt,
            Media = [media],
        };

        var result = report.ToResponse();

        Assert.AreEqual(reportId, result.Id);
        Assert.AreEqual(earthquakeId, result.EarthquakeId);
        Assert.AreEqual(report.Description, result.Description);
        Assert.AreEqual(report.Severity, result.Severity);
        Assert.AreEqual(report.DamageSigns, result.DamageSigns);
        Assert.AreEqual(StructureType.Apartment, result.StructureType);
        Assert.AreEqual(StructureSize.Large, result.StructureSize);
        Assert.AreEqual(report.Location.Y, result.Latitude);
        Assert.AreEqual(report.Location.X, result.Longitude);
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
            Location = GeoPoint.FromCoordinates(1, 2),
        };

        var result = report.ToResponse();

        Assert.IsNull(result.StructureType);
        Assert.IsNull(result.StructureSize);
        Assert.AreEqual(0, result.Media.Count);
    }

    [TestMethod]
    public void DamageReportSummaryResponseMapsReportFieldsWithoutMedia()
    {
        var report = new DamageReport
        {
            Id = Guid.NewGuid(),
            EarthquakeId = Guid.NewGuid(),
            Description = "Cracked building",
            Severity = SeverityLevel.Severe,
            DamageSigns = DamageSign.Cracks,
            StructureType = StructureType.Commercial,
            StructureSize = StructureSize.Medium,
            Location = GeoPoint.FromCoordinates(4.5, -74.3),
            Address = "Main Street",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = report.ToSummaryResponse();

        Assert.AreEqual(report.Id, result.Id);
        Assert.AreEqual(report.EarthquakeId, result.EarthquakeId);
        Assert.AreEqual(report.Description, result.Description);
        Assert.AreEqual(report.Severity, result.Severity);
        Assert.AreEqual(report.DamageSigns, result.DamageSigns);
        Assert.AreEqual(report.StructureType, result.StructureType);
        Assert.AreEqual(report.StructureSize, result.StructureSize);
        Assert.AreEqual(report.Location.Y, result.Latitude);
        Assert.AreEqual(report.Location.X, result.Longitude);
        Assert.AreEqual(report.Address, result.Address);
        Assert.AreEqual(report.CreatedAt, result.CreatedAt);
        Assert.IsNull(result.GetType().GetProperty("Media"));
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
        Assert.IsTrue(entity.GetIndexes().Any(index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(DamageReport.CreatedAt),
                nameof(DamageReport.Id)])));
        Assert.IsTrue(entity.GetIndexes().Any(index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(DamageReport.Severity),
                nameof(DamageReport.CreatedAt),
                nameof(DamageReport.Id)])));

        var seeded = db.Earthquakes.Single(e => e.Id == QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId);
        Assert.IsTrue(seeded.IsActive);
        Assert.AreEqual("M7.4 - Colombia", seeded.Name);
    }

    [TestMethod]
    public void BloodDonationCenterMapsSpanishSafePublicSummaryAndMapsUrl()
    {
        var center = new BloodDonationCenter { Id = Guid.NewGuid(), EarthquakeId = Guid.NewGuid(), Name = "Banco de sangre", Address = "Calle 1", OperatingInstructions = "Confirma antes de ir", NeedsSummary = "Donaciones", PublicPhone = "3001234567", Location = GeoPoint.FromCoordinates(4.5, -74.3), BloodTypes = BloodTypeFlags.APositive | BloodTypeFlags.ONegative, Components = BloodComponentFlags.Plasma, CenterType = BloodDonationCenterType.PermanentSite };
        var result = center.ToSummaryResponse();
        Assert.AreEqual(center.Id, result.Id);
        Assert.IsTrue(result.GoogleMapsUrl.Contains("4.5", StringComparison.Ordinal));
        Assert.AreEqual(center.BloodTypes, result.BloodTypes);
        Assert.AreEqual(center.Components, result.Components);
        Assert.IsNull(result.GetType().GetProperty("PublicPhone"));
    }

    [TestMethod]
    public void BloodDonationCenterModelHasRequiredIndexesAndCascadingComments()
    {
        using var db = TestDb.Create();
        var entity = db.Model.FindEntityType(typeof(BloodDonationCenter));
        Assert.IsNotNull(entity);
        Assert.IsNotNull(entity!.FindNavigation(nameof(BloodDonationCenter.Comments)));
        Assert.IsTrue(entity.GetIndexes().Any(index => index.Properties.Any(property => property.Name == nameof(BloodDonationCenter.SearchText))));
        Assert.IsTrue(entity.GetIndexes().Any(index => index.Properties.Any(property => property.Name == nameof(BloodDonationCenter.ManagementCodeHash))));
        var commentEntity = db.Model.FindEntityType(typeof(BloodDonationCenterComment));
        Assert.IsTrue(commentEntity!.GetForeignKeys().Any(fk => fk.DeleteBehavior == DeleteBehavior.Cascade));
    }
}
