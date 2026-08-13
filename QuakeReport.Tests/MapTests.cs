using System.Net;
using System.Net.Http.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuakeReport.ApiService.Controllers;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Map;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data.Geospatial;
using QuakeReport.Data.Models;
using QuakeReport.Web.Helpers;
using QuakeReport.Web.Services;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class MapTests
{
    [TestMethod]
    public async Task MapServiceReturnsEveryPublicGeolocatedLayerForActiveEarthquake()
    {
        await using var db = TestDb.Create();
        var earthquake = await db.Earthquakes.AsNoTracking().SingleAsync(item => item.IsActive);
        var otherEarthquake = CreateEarthquake("Other", false);

        var report = CreateDamageReport(earthquake.Id, "Visible report", 4.61, -74.08);
        var shelter = CreateShelter(earthquake.Id, "Visible shelter", 4.62, -74.09);
        var collectionPoint = CreateCollectionPoint(earthquake.Id, "Visible collection", 4.63, -74.10);
        var bloodCenter = CreateBloodCenter(earthquake.Id, "Visible blood", 4.64, -74.11);
        var helpRequest = CreateHelpRequest(earthquake.Id, "Visible help", 4.65, -74.12);
        var person = CreateMissingPerson(earthquake.Id, "Visible person");
        var personLocation = new MissingPersonLocation
        {
            Id = Guid.NewGuid(),
            MissingPersonId = person.Id,
            MissingPerson = person,
            Address = "Person address",
            Location = GeoPoint.FromCoordinates(4.66, -74.13),
        };

        db.AddRange(
            otherEarthquake,
            report,
            shelter,
            collectionPoint,
            bloodCenter,
            helpRequest,
            person,
            personLocation,
            CreateDamageReport(otherEarthquake.Id, "Other report", 1, 1),
            CreateShelter(
                earthquake.Id,
                "Rejected shelter",
                4.7,
                -74.2,
                ShelterModerationStatus.Rejected),
            CreateCollectionPoint(earthquake.Id, "No coordinates", null, null),
            CreateMissingPersonWithLocation(
                earthquake.Id,
                "Closed person",
                MissingPersonStatus.Closed));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await new MapService(db).GetOverviewAsync(
            earthquake,
            CancellationToken.None);

        Assert.AreEqual(7, result.Elements.Count);
        CollectionAssert.AreEquivalent(
            Enum.GetValues<MapElementType>(),
            result.Elements.Select(element => element.Type).ToArray());
        Assert.IsTrue(result.Elements.Any(element => element.MarkerId == personLocation.Id));
        Assert.IsTrue(result.Elements.Any(element =>
            element.EntityId == person.Id &&
            element.Type == MapElementType.MissingPerson));
        Assert.IsFalse(result.Elements.Any(element => element.Title == "Rejected shelter"));
        Assert.IsFalse(result.Elements.Any(element => element.Title == "No coordinates"));
        Assert.IsFalse(result.Elements.Any(element => element.Title == "Closed person"));
        Assert.IsFalse(result.Elements.Any(element => element.Title == "Other report"));
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
    }

    [TestMethod]
    public async Task MapControllerReturnsOverviewForActiveEarthquake()
    {
        await using var db = TestDb.Create();
        var controller = new MapController(
            new ActiveEarthquakeService(db),
            new MapService(db));

        var result = await controller.Get();
        var ok = TestAssert.InstanceOf<OkObjectResult>(result);
        var response = TestAssert.InstanceOf<MapOverviewResponse>(ok.Value);

        Assert.AreEqual(QuakeReport.Data.QuakeReportDbContext.ColombiaEarthquakeId, response.Earthquake.Id);
        Assert.AreEqual(MapElementType.Earthquake, response.Elements.Single().Type);
    }

    [TestMethod]
    public async Task MapControllerReturnsUnprocessableEntityWithoutActiveEarthquake()
    {
        await using var db = TestDb.Create();
        var active = await db.Earthquakes.SingleAsync(item => item.IsActive);
        active.IsActive = false;
        await db.SaveChangesAsync();

        var controller = new MapController(
            new ActiveEarthquakeService(db),
            new MapService(db));

        var result = await controller.Get();

        TestAssert.InstanceOf<UnprocessableEntityObjectResult>(result);
    }

    [TestMethod]
    public async Task ApiClientGetsAndDeserializesMapOverview()
    {
        var earthquake = new EarthquakeResponse(
            Guid.NewGuid(),
            "Earthquake",
            7.4,
            DateTimeOffset.UtcNow,
            4.5,
            -74.1);
        var expected = new MapOverviewResponse(earthquake, []);
        var handler = new MapRecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expected),
        });
        var client = new QuakeReportApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test"),
        });

        var result = await client.GetMapOverviewAsync();

        Assert.AreEqual(HttpMethod.Get, handler.Method);
        Assert.AreEqual("/api/map", handler.Uri?.PathAndQuery);
        Assert.AreEqual(earthquake.Id, result.Earthquake.Id);
    }

    [TestMethod]
    public void MapPresentationProvidesRoutesAndEncodesInfoWindowContent()
    {
        var id = Guid.NewGuid();
        var element = new MapElementResponse(
            Guid.NewGuid(),
            id,
            MapElementType.Shelter,
            "<script>alert('title')</script>",
            "Summary & details",
            "Street < 5",
            4.5,
            -74.1);

        var presentation = MapPresentation.For(element);
        var html = MapPresentation.InfoWindowHtml(element);

        Assert.AreEqual($"/refugios/{id}", presentation.DetailPath);
        StringAssert.Contains(html, "&lt;script&gt;");
        StringAssert.Contains(html, "Summary &amp; details");
        StringAssert.Contains(html, "Ver detalles");
        Assert.IsFalse(html.Contains("<script>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MapPresentationBuildsADataUrlIconContainingTheLayerStyle()
    {
        var element = Element(MapElementType.HelpRequest);

        var dataUrl = MapPresentation.MarkerIconDataUrl(element);
        var encodedSvg = dataUrl["data:image/svg+xml;base64,".Length..];
        var svg = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encodedSvg));
        var document = XDocument.Parse(svg);
        var glyph = document.Descendants()
            .Single(node => node.Name.LocalName == "text")
            .Value
            .Trim();

        StringAssert.StartsWith(dataUrl, "data:image/svg+xml;base64,");
        StringAssert.Contains(svg, "#f57c00");
        Assert.AreEqual("H", glyph);
        StringAssert.Contains(svg, "viewBox=\"0 0 44 52\"");
    }

    [TestMethod]
    public void MapPresentationAlwaysKeepsEpicenterAndFiltersOtherLayers()
    {
        var elements = new[]
        {
            Element(MapElementType.Earthquake),
            Element(MapElementType.Shelter),
            Element(MapElementType.HelpRequest),
        };

        var visible = MapPresentation.VisibleElements(
            elements,
            new HashSet<MapElementType> { MapElementType.Shelter }).ToList();

        CollectionAssert.AreEquivalent(
            new[] { MapElementType.Earthquake, MapElementType.Shelter },
            visible.Select(element => element.Type).ToArray());
    }

    private static MapElementResponse Element(MapElementType type) =>
        new(Guid.NewGuid(), Guid.NewGuid(), type, type.ToString(), null, null, 0, 0);

    private static Earthquake CreateEarthquake(string name, bool active) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Magnitude = 6,
            OccurredAt = DateTimeOffset.UtcNow,
            Location = GeoPoint.FromCoordinates(2, 2),
            IsActive = active,
        };

    private static DamageReport CreateDamageReport(
        Guid earthquakeId,
        string description,
        double latitude,
        double longitude) =>
        new()
        {
            Id = Guid.NewGuid(),
            EarthquakeId = earthquakeId,
            Description = description,
            Severity = SeverityLevel.Major,
            DamageSigns = DamageSign.Cracks,
            Location = GeoPoint.FromCoordinates(latitude, longitude),
        };

    private static Shelter CreateShelter(
        Guid earthquakeId,
        string name,
        double? latitude,
        double? longitude,
        ShelterModerationStatus moderationStatus = ShelterModerationStatus.Pending) =>
        new()
        {
            Id = Guid.NewGuid(),
            EarthquakeId = earthquakeId,
            Name = name,
            Address = $"{name} address",
            Location = GeoPoint.FromCoordinates(latitude, longitude),
            Description = "Description",
            OperatingInstructions = "Instructions",
            ModerationStatus = moderationStatus,
        };

    private static CollectionPoint CreateCollectionPoint(
        Guid earthquakeId,
        string name,
        double? latitude,
        double? longitude) =>
        new()
        {
            Id = Guid.NewGuid(),
            EarthquakeId = earthquakeId,
            Name = name,
            Address = $"{name} address",
            Location = GeoPoint.FromCoordinates(latitude, longitude),
            NeedsSummary = "Needs",
            ReceivingInstructions = "Instructions",
        };

    private static BloodDonationCenter CreateBloodCenter(
        Guid earthquakeId,
        string name,
        double latitude,
        double longitude) =>
        new()
        {
            Id = Guid.NewGuid(),
            EarthquakeId = earthquakeId,
            Name = name,
            Address = $"{name} address",
            Location = GeoPoint.FromCoordinates(latitude, longitude),
            OperatingInstructions = "Instructions",
            NeedsSummary = "Needs",
            PublicPhone = "555",
        };

    private static HelpRequest CreateHelpRequest(
        Guid earthquakeId,
        string title,
        double latitude,
        double longitude) =>
        new()
        {
            Id = Guid.NewGuid(),
            EarthquakeId = earthquakeId,
            Title = title,
            RequesterName = "Requester",
            Address = $"{title} address",
            Location = GeoPoint.FromCoordinates(latitude, longitude),
            NeedDetails = "Needs",
            PublicPhone = "555",
            Priority = HelpRequestPriority.High,
            NeedCategories = HelpNeedCategory.FoodAndWater,
        };

    private static MissingPerson CreateMissingPerson(Guid earthquakeId, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            EarthquakeId = earthquakeId,
            FullName = name,
            Description = "Description",
            LastSeenAt = DateTimeOffset.UtcNow,
            ManagementCodeHash = Guid.NewGuid().ToString("N"),
            PublicationConsentAt = DateTimeOffset.UtcNow,
        };

    private static MissingPerson CreateMissingPersonWithLocation(
        Guid earthquakeId,
        string name,
        MissingPersonStatus status)
    {
        var person = CreateMissingPerson(earthquakeId, name);
        person.Status = status;
        person.Locations.Add(new MissingPersonLocation
        {
            Id = Guid.NewGuid(),
            MissingPersonId = person.Id,
            Address = "Closed address",
            Location = GeoPoint.FromCoordinates(5, -74),
        });
        return person;
    }

    private sealed class MapRecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? Uri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri;
            return Task.FromResult(response);
        }
    }
}
