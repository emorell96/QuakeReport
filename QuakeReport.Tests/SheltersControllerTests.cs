using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QuakeReport.ApiService.Controllers;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.ApiService.Security;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;
using StorageGenerics.Core.Models;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class SheltersControllerTests
{
    [TestMethod]
    public async Task CreateAssignsActiveEarthquakeAndReturnsOneTimeCode()
    {
        using var db = TestDb.Create();
        var created = TestAssert.InstanceOf<CreatedAtActionResult>(await Controller(db).Create(CreateRequest("Refugio Central"), CancellationToken.None));
        var response = TestAssert.InstanceOf<CreateShelterResponse>(created.Value);
        var shelter = db.Shelters.Single();

        Assert.AreEqual(QuakeReportDbContext.ColombiaEarthquakeId, shelter.EarthquakeId);
        Assert.AreEqual(ShelterModerationStatus.Pending, shelter.ModerationStatus);
        Assert.AreEqual(ShelterOperationalStatus.Open, shelter.OperationalStatus);
        Assert.AreEqual(ShelterSource.Community, shelter.Source);
        Assert.IsFalse(string.IsNullOrWhiteSpace(response.ManagementCode));
        Assert.AreNotEqual(response.ManagementCode, shelter.ManagementCodeHash);
    }

    [TestMethod]
    public async Task ListIncludesPendingButExcludesRejectedAndClosedByDefault()
    {
        using var db = TestDb.Create();
        db.Shelters.AddRange(
            TestShelter("Pendiente", ShelterModerationStatus.Pending, ShelterOperationalStatus.Open),
            TestShelter("Rechazado", ShelterModerationStatus.Rejected, ShelterOperationalStatus.Open),
            TestShelter("Cerrado", ShelterModerationStatus.Approved, ShelterOperationalStatus.Closed));
        await db.SaveChangesAsync();

        var result = await Controller(db).List(page: 1, pageSize: 20, cancellationToken: CancellationToken.None);
        var response = TestAssert.InstanceOf<PagedResult<ShelterSummaryResponse>>(TestAssert.InstanceOf<OkObjectResult>(result).Value);

        Assert.AreEqual(1, response.TotalMatches);
        Assert.AreEqual("Pendiente", response.Results.Single().Name);
    }

    [TestMethod]
    public async Task OwnerCanOverwriteGoogleAddressAndCoordinatesAreRemoved()
    {
        using var db = TestDb.Create();
        var controller = Controller(db);
        var created = TestAssert.InstanceOf<CreateShelterResponse>(TestAssert.InstanceOf<CreatedAtActionResult>(await controller.Create(CreateRequest("Refugio"), CancellationToken.None)).Value);

        var update = new UpdateShelterRequest("Refugio", null, "Dirección escrita manualmente", null, null, "Descripción", "Abierto todo el día", null, null, null, null);
        var result = await controller.Update(created.Shelter.Id, update, created.ManagementCode, CancellationToken.None);
        var response = TestAssert.InstanceOf<ShelterResponse>(TestAssert.InstanceOf<OkObjectResult>(result).Value);

        Assert.IsNull(response.Latitude);
        Assert.IsNull(response.Longitude);
        StringAssert.Contains(response.GoogleMapsUrl, "Direcci%C3%B3n%20escrita%20manualmente");
    }

    [TestMethod]
    public async Task ModeratorCanEditApproveAndAuditShelter()
    {
        using var db = TestDb.Create();
        var controller = Controller(db);
        var created = TestAssert.InstanceOf<CreateShelterResponse>(TestAssert.InstanceOf<CreatedAtActionResult>(await controller.Create(CreateRequest("Original"), CancellationToken.None)).Value);
        var edit = new UpdateShelterRequest("Modificado", "Alcaldía", "Calle 2", null, null, "Nueva descripción", "Horario nuevo", null, null, null, null);

        var edited = await controller.ModeratorUpdate(created.Shelter.Id, edit, "moderator-secret", CancellationToken.None);
        Assert.AreEqual("Modificado", TestAssert.InstanceOf<ShelterResponse>(TestAssert.InstanceOf<OkObjectResult>(edited).Value).Name);
        await controller.Moderate(created.Shelter.Id, new(ShelterModerationStatus.Approved), "moderator-secret", "mod@test.com", CancellationToken.None);

        var shelter = db.Shelters.Single();
        Assert.AreEqual(ShelterModerationStatus.Approved, shelter.ModerationStatus);
        Assert.AreEqual("mod@test.com", shelter.ModeratedBy);
        Assert.IsNotNull(shelter.ModeratedAt);
    }

    [TestMethod]
    public void ModelContainsShelterIndexesAndRelationships()
    {
        using var db = TestDb.Create();
        var entity = db.Model.FindEntityType(typeof(Shelter))!;
        Assert.IsTrue(entity.GetIndexes().Any(index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(Shelter.EarthquakeId), nameof(Shelter.ModerationStatus), nameof(Shelter.OperationalStatus), nameof(Shelter.CreatedAt)])));
        Assert.IsTrue(entity.GetIndexes().Any(index => index.Properties.SingleOrDefault()?.Name == nameof(Shelter.ManagementCodeHash) && index.IsUnique));
        Assert.AreEqual(Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade, db.Model.FindEntityType(typeof(ShelterAbuseReport))!.GetForeignKeys().Single().DeleteBehavior);
    }

    private static SheltersController Controller(QuakeReportDbContext db) => new(
        db, new ActiveEarthquakeService(db), new AlwaysTurnstile(),
        new ModerationKeyValidator(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Moderation:ApiKey"] = "moderator-secret" }).Build()),
        TestRepository.Create<Shelter>(db));

    private static CreateShelterRequest CreateRequest(string name) =>
        new(name, null, "Calle 1", 3.45, -76.53, "Descripción", "Abierto todo el día", null, null, null, null, true, "token");

    private static Shelter TestShelter(string name, ShelterModerationStatus moderation, ShelterOperationalStatus operational) => new()
    {
        Id = Guid.NewGuid(), EarthquakeId = QuakeReportDbContext.ColombiaEarthquakeId, Name = name,
        Address = "Calle 1", SearchText = name.ToUpperInvariant(), Description = "Descripción",
        OperatingInstructions = "Abierto", ModerationStatus = moderation, OperationalStatus = operational,
        Source = ShelterSource.Community, ManagementCodeHash = Guid.NewGuid().ToString("N"),
    };

    private sealed class AlwaysTurnstile : ITurnstileValidator
    {
        public Task<TurnstileValidationResult> ValidateAsync(string? token, CancellationToken cancellationToken) =>
            Task.FromResult(new TurnstileValidationResult(true));
    }
}
