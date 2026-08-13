using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using QuakeReport.ApiService.Controllers;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Ingestion;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class IngestionControllerTests
{
    [TestMethod]
    public async Task IngestionCreatesPendingAutomatedCollectionPointWithoutTurnstile()
    {
        using var db = TestDb.Create();
        var controller = Controller(db, "ingestion-secret");
        SetHeaders(controller, "ingestion-secret", "social-post-1");

        var result = await controller.CollectionPoint(new(
            new(IngestionPlatform.X, "https://x.com/example/status/1", "1", null, null, 0.9m, "Public post"),
            new("Centro comunitario", null, "Calle 1, Cali", null, null, null, "Agua", "Recibir en horario laboral", null, null, null, null, null)), CancellationToken.None);

        var created = TestAssert.InstanceOf<CreatedResult>(result);
        var response = TestAssert.InstanceOf<IngestionSubmissionResponse>(created.Value);
        Assert.IsFalse(response.Duplicate);
        Assert.AreEqual(CollectionPointSource.Automated, db.CollectionPoints.Single().Source);
        Assert.AreEqual(CollectionPointModerationStatus.Pending, db.CollectionPoints.Single().ModerationStatus);
        Assert.AreEqual(QuakeReportDbContext.ColombiaEarthquakeId, db.CollectionPoints.Single().EarthquakeId);
        Assert.AreEqual(1, db.IngestionSubmissions.Count());
    }

    [TestMethod]
    public async Task ReplayingIdempotencyKeyReturnsOriginalSubmissionWithoutCreatingAnotherEntity()
    {
        using var db = TestDb.Create();
        var controller = Controller(db, "ingestion-secret");
        var request = new IngestionCollectionPointRequest(
            new(IngestionPlatform.Website, "https://example.com/post/1", null, null, null, 1m, null),
            new("Centro", null, "Calle 1", null, null, null, "Agua", "Recibir", null, null, null, null, null));

        SetHeaders(controller, "ingestion-secret", "same-key");
        var first = TestAssert.InstanceOf<CreatedResult>(await controller.CollectionPoint(request, CancellationToken.None));
        SetHeaders(controller, "ingestion-secret", "same-key");
        var second = TestAssert.InstanceOf<OkObjectResult>(await controller.CollectionPoint(request, CancellationToken.None));

        Assert.IsNotNull(first.Value);
        var response = TestAssert.InstanceOf<IngestionSubmissionResponse>(second.Value);
        Assert.IsTrue(response.Duplicate);
        Assert.AreEqual(1, db.CollectionPoints.Count());
        Assert.AreEqual(1, db.IngestionSubmissions.Count());
    }

    [TestMethod]
    public async Task InvalidApiKeyAndMissingIdempotencyKeyAreRejected()
    {
        using var db = TestDb.Create();
        var controller = Controller(db, "ingestion-secret");
        SetHeaders(controller, "wrong", "key");
        var request = new IngestionCollectionPointRequest(
            new(IngestionPlatform.X, "https://x.com/example/status/2", null, null, null, 0.5m, null),
            new("Centro", null, "Calle 1", null, null, null, "Agua", "Recibir", null, null, null, null, null));

        Assert.IsInstanceOfType(
            TestAssert.Unwrap(await controller.CollectionPoint(request, CancellationToken.None)),
            typeof(UnauthorizedResult));
        SetHeaders(controller, "ingestion-secret", null);
        Assert.IsInstanceOfType(
            TestAssert.Unwrap(await controller.CollectionPoint(request, CancellationToken.None)),
            typeof(BadRequestObjectResult));
    }

    private static IngestionController Controller(QuakeReport.Data.QuakeReportDbContext db, string key) =>
        new(
            new IngestionPersistenceService(db),
            new ActiveEarthquakeService(db),
            new IngestionApiKeyValidator(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Ingestion:ApiKey"] = key }).Build()));

    private static void SetHeaders(IngestionController controller, string key, string? idempotency)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Ingestion-Api-Key"] = key;
        if (idempotency is not null) context.Request.Headers["Idempotency-Key"] = idempotency;
        controller.ControllerContext = new ControllerContext { HttpContext = context };
    }
}
