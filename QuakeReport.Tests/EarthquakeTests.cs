using Microsoft.AspNetCore.Mvc;
using QuakeReport.ApiService.Controllers;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Dtos;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Data.Models;
using QuakeReport.Data.Geospatial;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class EarthquakeTests
{
    [TestMethod]
    public async Task ActiveEarthquakeServiceReturnsActiveEarthquake()
    {
        using var db = TestDb.Create();
        var service = new ActiveEarthquakeService(db);

        var result = await service.GetActiveEarthquakeAsync(CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual(QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId, result!.Id);
    }

    [TestMethod]
    public async Task ActiveEarthquakeServiceReturnsNullWhenNothingIsActive()
    {
        using var db = TestDb.Create();
        db.Earthquakes.RemoveRange(db.Earthquakes);
        await db.SaveChangesAsync();
        var service = new ActiveEarthquakeService(db);

        var result = await service.GetActiveEarthquakeAsync(CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ActiveEarthquakeServiceRejectsMultipleActiveEarthquakes()
    {
        using var db = TestDb.Create();
        db.Earthquakes.Add(new Earthquake
        {
            Id = Guid.NewGuid(),
            Name = "Second event",
            Magnitude = 5.1,
            OccurredAt = DateTimeOffset.UtcNow,
            Location = GeoPoint.FromCoordinates(0, 0),
            IsActive = true,
        });
        await db.SaveChangesAsync();
        var service = new ActiveEarthquakeService(db);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.GetActiveEarthquakeAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task EarthquakesControllerReturnsActiveEarthquakeResponse()
    {
        using var db = TestDb.Create();
        var controller = new EarthquakesController(new ActiveEarthquakeService(db));

        var result = await controller.GetActive(CancellationToken.None);

        var ok = TestAssert.InstanceOf<OkObjectResult>(result);
        var response = TestAssert.InstanceOf<EarthquakeResponse>(ok.Value);
        Assert.AreEqual(QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId, response.Id);
        Assert.AreEqual("M7.4 - Colombia", response.Name);
    }

    [TestMethod]
    public async Task EarthquakesControllerReturnsNotFoundWhenNothingIsActive()
    {
        using var db = TestDb.Create();
        db.Earthquakes.RemoveRange(db.Earthquakes);
        await db.SaveChangesAsync();
        var controller = new EarthquakesController(new ActiveEarthquakeService(db));

        var result = await controller.GetActive(CancellationToken.None);

        TestAssert.InstanceOf<NotFoundResult>(result);
    }
}
