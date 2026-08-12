using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using QuakeReport.ApiService.Controllers;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class CollectionPointsControllerTests
{
    [TestMethod]
    public async Task CreateAssignsActiveEarthquakeAndReturnsOneTimeManagementCode()
    {
        using var db = TestDb.Create();
        var result = await Controller(db).Create(Request("Centro Cali"), CancellationToken.None);
        var created = TestAssert.InstanceOf<CreatedAtActionResult>(result);
        var payload = TestAssert.InstanceOf<CreateCollectionPointResponse>(created.Value);
        Assert.IsFalse(string.IsNullOrWhiteSpace(payload.ManagementCode));

        var point = db.CollectionPoints.Single(point => point.Name == "Centro Cali");
        Assert.AreEqual(QuakeReportDbContext.ColombiaEarthquakeId, point.EarthquakeId);
        Assert.AreEqual(CollectionPointModerationStatus.Pending, point.ModerationStatus);
        Assert.AreEqual(CollectionPointSource.Community, point.Source);
        Assert.AreNotEqual(payload.ManagementCode, point.ManagementCodeHash);
    }

    [TestMethod]
    public async Task ListFiltersBeforePaginationAndHidesRejectedAndClosedByDefault()
    {
        using var db = TestDb.Create();
        var earthquakeId = QuakeReportDbContext.ColombiaEarthquakeId;
        db.CollectionPoints.AddRange(
            Point(earthquakeId, "Pendiente", CollectionPointModerationStatus.Pending, CollectionPointOperationalStatus.Open),
            Point(earthquakeId, "Rechazado", CollectionPointModerationStatus.Rejected, CollectionPointOperationalStatus.Open),
            Point(earthquakeId, "Cerrado", CollectionPointModerationStatus.Approved, CollectionPointOperationalStatus.Closed));
        await db.SaveChangesAsync();

        var result = await Controller(db).List(page: 1, pageSize: 1, cancellationToken: CancellationToken.None);
        var page = TestAssert.InstanceOf<PagedResponse<CollectionPointSummaryResponse>>(TestAssert.InstanceOf<OkObjectResult>(result).Value);

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual("Pendiente", page.Items.Single().Name);
    }

    [TestMethod]
    public async Task ManagementCodeCanUpdateAndHideComment()
    {
        using var db = TestDb.Create();
        var controller = Controller(db);
        var created = TestAssert.InstanceOf<CreatedAtActionResult>(await controller.Create(Request("Centro"), CancellationToken.None));
        var payload = TestAssert.InstanceOf<CreateCollectionPointResponse>(created.Value);
        var code = payload.ManagementCode;
        var point = db.CollectionPoints.Single(point => point.Name == "Centro");

        var comment = TestAssert.InstanceOf<CreatedResult>(await controller.CreateComment(point.Id, new("Vecino", "Necesitan agua", "ok"), CancellationToken.None)).Value;
        var commentResponse = TestAssert.InstanceOf<CollectionPointCommentResponse>(comment);
        var update = await controller.Update(point.Id, new("Centro actualizado", null, "Calle 2", null, null, null, "Agua", "Recibir de 8 a 5", null, null, null, null, null), code, CancellationToken.None);
        Assert.IsInstanceOfType(update, typeof(OkObjectResult));

        var hidden = await controller.HideComment(point.Id, commentResponse.Id, code, CancellationToken.None);
        Assert.IsInstanceOfType(hidden, typeof(NoContentResult));
        Assert.IsTrue(db.CollectionPointComments.Single().IsHidden);
    }

    [TestMethod]
    public async Task MapsUrlUsesCoordinatesOrEncodedAddress()
    {
        using var db = TestDb.Create();
        var earthquakeId = QuakeReportDbContext.ColombiaEarthquakeId;
        var coordinates = Point(earthquakeId, "Coordenadas", CollectionPointModerationStatus.Approved, CollectionPointOperationalStatus.Open);
        coordinates.Latitude = 3.4516;
        coordinates.Longitude = -76.5320;
        var address = Point(earthquakeId, "Dirección", CollectionPointModerationStatus.Approved, CollectionPointOperationalStatus.Open);
        address.Latitude = null;
        address.Longitude = null;
        db.CollectionPoints.AddRange(coordinates, address);
        await db.SaveChangesAsync();

        var coordinatePage = TestAssert.InstanceOf<PagedResponse<CollectionPointSummaryResponse>>(TestAssert.InstanceOf<OkObjectResult>(await Controller(db).List(query: "Coordenadas", cancellationToken: CancellationToken.None)).Value);
        var coordinateResponse = coordinatePage.Items.Single();
        Assert.AreEqual("https://www.google.com/maps/search/?api=1&query=3.4516,-76.532", coordinateResponse.GoogleMapsUrl);
        var addressPage = TestAssert.InstanceOf<PagedResponse<CollectionPointSummaryResponse>>(TestAssert.InstanceOf<OkObjectResult>(await Controller(db).List(query: "Direcci", cancellationToken: CancellationToken.None)).Value);
        var addressResponse = addressPage.Items.Single();
        StringAssert.Contains(addressResponse.GoogleMapsUrl, "google.com/maps/search/?api=1&query=");
    }

    [TestMethod]
    public async Task ListWithUserLocationReturnsNearestGeocodedPointFirst()
    {
        using var db = TestDb.Create();
        var earthquakeId = QuakeReportDbContext.ColombiaEarthquakeId;
        var nearby = Point(earthquakeId, "Cerca", CollectionPointModerationStatus.Approved, CollectionPointOperationalStatus.Open);
        nearby.Latitude = 3.4516;
        nearby.Longitude = -76.5320;
        var farAway = Point(earthquakeId, "Lejos", CollectionPointModerationStatus.Approved, CollectionPointOperationalStatus.Open);
        farAway.Latitude = 4.7110;
        farAway.Longitude = -74.0721;
        var withoutCoordinates = Point(earthquakeId, "Sin coordenadas", CollectionPointModerationStatus.Approved, CollectionPointOperationalStatus.Open);
        db.CollectionPoints.AddRange(farAway, withoutCoordinates, nearby);
        await db.SaveChangesAsync();

        var result = await Controller(db).List(pageSize: 1, cancellationToken: CancellationToken.None, latitude: 3.45, longitude: -76.53);
        var page = TestAssert.InstanceOf<PagedResponse<CollectionPointSummaryResponse>>(TestAssert.InstanceOf<OkObjectResult>(result).Value);

        Assert.AreEqual(2, page.TotalCount);
        Assert.AreEqual("Cerca", page.Items.Single().Name);
    }

    private static CollectionPointsController Controller(QuakeReportDbContext db) => new(
        db,
        new ActiveEarthquakeService(db),
        new AlwaysTurnstile(),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Moderation:ApiKey"] = "moderator-secret" }).Build());

    private static CreateCollectionPointRequest Request(string name) => new(name, null, "Calle 1, Cali", null, null, null, "Agua y alimentos", "Recibir de 8 a 5", null, null, null, null, null, true, "ok");

    private static CollectionPoint Point(Guid earthquakeId, string name, CollectionPointModerationStatus moderation, CollectionPointOperationalStatus operational) => new()
    {
        Id = Guid.NewGuid(), EarthquakeId = earthquakeId, Name = name, Address = "Calle 1", SearchText = name.ToUpperInvariant(), NeedsSummary = "Agua", ReceivingInstructions = "Recibir", ModerationStatus = moderation, OperationalStatus = operational, Source = CollectionPointSource.Community,
        ManagementCodeHash = "hash", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
    };

    private sealed class AlwaysTurnstile : ITurnstileValidator
    {
        public Task<TurnstileValidationResult> ValidateAsync(string? token, CancellationToken cancellationToken) => Task.FromResult(new TurnstileValidationResult(true));
    }
}
