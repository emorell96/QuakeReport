using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using QuakeReport.Data;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data.Geospatial;
using QuakeReport.Data.Models;
using QuakeReport.Geospatial;

namespace QuakeReport.Tests;

[TestClass]
public class GeocodingTests
{
    [TestMethod]
    public void PointFactoryUsesLongitudeThenLatitudeAndSrid4326()
    {
        var point = GeoPoint.FromCoordinates(4.5709, -74.2973);

        Assert.AreEqual(-74.2973, point.X);
        Assert.AreEqual(4.5709, point.Y);
        Assert.AreEqual(4326, point.SRID);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GeoPoint.FromCoordinates(91, 0));
    }

    [TestMethod]
    public void NearestQueryTranslatesToPostgresKnnAndExcludesNullLocations()
    {
        var options = new DbContextOptionsBuilder<QuakeReportDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test;Password=test",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;
        using var db = new QuakeReportDbContext(options);

        var sql = db.CollectionPoints
            .OrderByDistanceFrom(GeoPoint.FromCoordinates(4.61, -74.08))
            .ToQueryString();

        StringAssert.Contains(sql, "IS NOT NULL");
        StringAssert.Contains(sql, "<->");
    }

    [TestMethod]
    public async Task WorkerAutomaticallySavesOneHighConfidenceResult()
    {
        await using var db = TestDb.Create();
        var entity = CreateCollectionPoint("Calle 1, Bogotá");
        db.CollectionPoints.Add(entity);
        await db.SaveChangesAsync();
        var google = new FakeGeocoder(new GoogleGeocodingOutcome([
            new(4.61, -74.08, "Calle 1, Bogotá", "place-1", "ROOFTOP", false),
        ]));

        var result = await new GeocodingCoordinator(db, google, Configuration()).RunAsync();

        Assert.AreEqual(1, result.Located);
        Assert.AreEqual(-74.08, entity.Location!.X);
        Assert.AreEqual(4.61, entity.Location.Y);
        Assert.AreEqual(0, db.GeocodingReviewItems.Count());
    }

    [TestMethod]
    public async Task WorkerQueuesNoMatchAndDoesNotRepeatAnUnchangedAddress()
    {
        await using var db = TestDb.Create();
        var entity = CreateCollectionPoint("Dirección desconocida");
        db.CollectionPoints.Add(entity);
        await db.SaveChangesAsync();
        var google = new FakeGeocoder(new GoogleGeocodingOutcome([]));
        var coordinator = new GeocodingCoordinator(db, google, Configuration());

        var first = await coordinator.RunAsync();
        var second = await coordinator.RunAsync();

        Assert.AreEqual(1, first.Queued);
        Assert.AreEqual(1, second.Skipped);
        Assert.AreEqual(1, google.CallCount);
        var review = db.GeocodingReviewItems.Single();
        Assert.AreEqual(GeocodingReviewStatus.NeedsReview, review.Status);
        Assert.AreEqual(1, review.AttemptCount);
    }

    [TestMethod]
    public async Task WorkerRetriesProviderErrorsAndResolvesThemOnSuccess()
    {
        await using var db = TestDb.Create();
        var entity = CreateCollectionPoint("Carrera 7, Bogotá");
        db.CollectionPoints.Add(entity);
        await db.SaveChangesAsync();
        var google = new FakeGeocoder(
            new GoogleGeocodingOutcome([], "Google returned HTTP 503."),
            new GoogleGeocodingOutcome([new(4.65, -74.06, "Carrera 7", "place-2", "RANGE_INTERPOLATED", false)]));
        var coordinator = new GeocodingCoordinator(db, google, Configuration());

        await coordinator.RunAsync();
        var second = await coordinator.RunAsync();

        Assert.AreEqual(1, second.Located);
        Assert.AreEqual(GeocodingReviewStatus.Resolved, db.GeocodingReviewItems.Single().Status);
        Assert.AreEqual(2, db.GeocodingReviewItems.Single().AttemptCount);
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Geocoding:BatchSize"] = "100",
            ["Geocoding:MaxConcurrency"] = "2",
        }).Build();

    private static CollectionPoint CreateCollectionPoint(string address) => new()
    {
        Id = Guid.NewGuid(), EarthquakeId = Guid.NewGuid(), Name = "Punto de prueba",
        Address = address, NeedsSummary = "Agua", ReceivingInstructions = "Entrada principal",
    };

    private sealed class FakeGeocoder(params GoogleGeocodingOutcome[] outcomes) : IGoogleGeocoder
    {
        public int CallCount { get; private set; }

        public Task<GoogleGeocodingOutcome> GeocodeAsync(string address, CancellationToken cancellationToken = default)
        {
            var outcome = outcomes[Math.Min(CallCount, outcomes.Length - 1)];
            CallCount++;
            return Task.FromResult(outcome);
        }
    }
}
