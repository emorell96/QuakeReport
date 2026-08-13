using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using QuakeReport.ApiService.Controllers;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Media;
using QuakeReport.ApiService.MissingPeople;
using QuakeReport.Contracts.Dtos;
using QuakeReport.Contracts.Enums;
using QuakeReport.Data;
using QuakeReport.Data.Models;
using StorageGenerics.Core.Models;

namespace QuakeReport.Tests;

[TestClass]
[TestCategory("Unit")]
public class MissingPeopleControllerTests
{
    [TestMethod]
    public async Task CreateStoresOnlyDocumentHashAndReturnsManagementCodeOnce()
    {
        using var db = TestDb.Create();
        var controller = CreateController(db);
        var request = Request("Ana García", "CC 1234-5678");

        var result = await controller.Create(request, CancellationToken.None);
        var response = TestAssert.InstanceOf<CreateMissingPersonResponse>(TestAssert.InstanceOf<CreatedAtActionResult>(result).Value);
        var person = await db.MissingPeople.FindAsync(response.Person.Id);

        Assert.IsFalse(person!.IdentificationNumberHash!.Contains("1234"));
        Assert.IsNull(typeof(MissingPersonResponse).GetProperty("IdentificationNumberHash"));
        Assert.AreEqual("5678", response.Person.IdentificationLastFour);
        Assert.IsFalse(string.IsNullOrWhiteSpace(response.ManagementCode));
        Assert.AreNotEqual(response.ManagementCode, person.ManagementCodeHash);
    }

    [TestMethod]
    public async Task ListDefaultsToMissingAndFiltersBeforePagination()
    {
        using var db = TestDb.Create();
        var earthquakeId = QuakeReportDbContext.ColombiaEarthquakeId;
        db.MissingPeople.AddRange(Person(earthquakeId, "Missing one", MissingPersonStatus.Missing), Person(earthquakeId, "Found one", MissingPersonStatus.Found));
        await db.SaveChangesAsync();

        var result = await CreateController(db).List(page: 1, pageSize: 1, cancellationToken: CancellationToken.None);
        var page = TestAssert.InstanceOf<PagedResult<MissingPersonSummaryResponse>>(TestAssert.InstanceOf<OkObjectResult>(result).Value);

        Assert.AreEqual(1, page.TotalMatches);
        Assert.AreEqual("Missing one", page.Results.Single().FullName);
    }

    [TestMethod]
    public async Task LookupRequiresExactDocumentAndReturnsPublicDetailsOnly()
    {
        using var db = TestDb.Create();
        var controller = CreateController(db);
        var create = TestAssert.InstanceOf<CreateMissingPersonResponse>(TestAssert.InstanceOf<CreatedAtActionResult>(await controller.Create(Request("Persona", "CC12345678"), CancellationToken.None)).Value);

        var result = await controller.Lookup(new(IdentificationDocumentType.ColombianCitizenId, "CC 1234 5678", "token"), CancellationToken.None);
        var response = TestAssert.InstanceOf<MissingPersonResponse>(TestAssert.InstanceOf<OkObjectResult>(result).Value);

        Assert.AreEqual(create.Person.Id, response.Id);
        Assert.IsNull(typeof(MissingPersonResponse).GetProperty("ManagementCodeHash"));
        Assert.IsNull(typeof(MissingPersonResponse).GetProperty("IdentificationNumberHash"));
    }

    [TestMethod]
    public async Task OwnerCanHideTipButPublicResponseExcludesContacts()
    {
        using var db = TestDb.Create();
        var controller = CreateController(db);
        var create = TestAssert.InstanceOf<CreateMissingPersonResponse>(TestAssert.InstanceOf<CreatedAtActionResult>(await controller.Create(Request("Persona", null), CancellationToken.None)).Value);
        var tip = TestAssert.InstanceOf<MissingPersonTipResponse>(TestAssert.InstanceOf<CreatedResult>(await controller.CreateTip(create.Person.Id, new("Lo vi", DateTimeOffset.UtcNow, "Calle 1", null, null, "Privado", "300", "a@test.com", "token"), CancellationToken.None)).Value);

        Assert.IsNull(typeof(MissingPersonTipResponse).GetProperty("ResponderEmail"));
        var hidden = await controller.HideTip(create.Person.Id, tip.Id, create.ManagementCode, CancellationToken.None);
        Assert.IsInstanceOfType(hidden, typeof(NoContentResult));
        var publicTips = TestAssert.InstanceOf<PagedResult<MissingPersonTipResponse>>(TestAssert.InstanceOf<OkObjectResult>(await controller.Tips(create.Person.Id, cancellationToken: CancellationToken.None)).Value);
        Assert.AreEqual(0, publicTips.TotalMatches);
    }

    private static MissingPeopleController CreateController(QuakeReportDbContext db) =>
        new(
            new MissingPersonService(
                db,
                TestRepository.Create<MissingPerson>(db),
                TestRepository.Create<MissingPersonTip>(db)),
            new ActiveEarthquakeService(db),
            new MissingPersonSecurity(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MissingPeople:IdHmacKey"] = "test-secret" }).Build()),
            new AlwaysTurnstile(),
            new NoopPhotoStorage());

    private static CreateMissingPersonRequest Request(string name, string? document) =>
        new(name, null, "30", document is null ? null : IdentificationDocumentType.ColombianCitizenId, document, "Descripción", null, null, DateTimeOffset.UtcNow.AddHours(-1), [new("Bogotá", null, null, null)], true, "token");

    private static MissingPerson Person(Guid earthquakeId, string name, MissingPersonStatus status) => new()
    {
        Id = Guid.NewGuid(), EarthquakeId = earthquakeId, FullName = name, SearchName = name.ToUpperInvariant(), Description = "Descripción", LastSeenAt = DateTimeOffset.UtcNow.AddHours(-1), Status = status, ManagementCodeHash = "00", PublicationConsentAt = DateTimeOffset.UtcNow,
        Locations = [new MissingPersonLocation { Id = Guid.NewGuid(), MissingPersonId = Guid.Empty, Address = "Bogotá", SearchAddress = "BOGOTA" }]
    };

    private sealed class AlwaysTurnstile : ITurnstileValidator
    {
        public Task<TurnstileValidationResult> ValidateAsync(string? token, CancellationToken cancellationToken) => Task.FromResult(new TurnstileValidationResult(true));
    }

    private sealed class NoopPhotoStorage : IMissingPersonPhotoStorage
    {
        public Task<string> UploadAsync(Guid personId, string extension, Stream content, string contentType, CancellationToken cancellationToken) => Task.FromResult($"https://test/{personId}{extension}");
    }
}
