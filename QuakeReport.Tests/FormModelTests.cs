using FluentValidation;
using Microsoft.AspNetCore.Components.Forms;
using QuakeReport.Contracts.Enums;
using QuakeReport.Web.Models;

namespace QuakeReport.Tests;

[TestClass]
public sealed class FormModelTests
{
    [TestMethod]
    public async Task SharedCapabilities_ValidateConsentTurnstileAndContactFormats()
    {
        var model = new CapabilityForm
        {
            Address = "Bogotá",
            Phone = "+57 (601) 234-5678",
            WhatsApp = "+57 300.123.4567",
            Email = "persona@example.com",
            PrivacyConsent = true,
            TurnstileToken = "token",
        };

        var validator = new CapabilityFormValidator();
        Assert.IsTrue((await validator.ValidateAsync(model)).IsValid);

        model.Phone = "123";
        model.Email = "correo inválido";
        model.PrivacyConsent = false;
        model.TurnstileToken = string.Empty;
        var invalid = await validator.ValidateAsync(model);

        AssertHasError(invalid, nameof(model.Phone));
        AssertHasError(invalid, nameof(model.Email));
        AssertHasError(invalid, nameof(model.PrivacyConsent));
        AssertHasError(invalid, nameof(model.TurnstileToken));
    }

    [TestMethod]
    public async Task SharedLocationRules_RequireCoordinatePairsAndValidateRanges()
    {
        var validator = new CapabilityFormValidator();
        var model = ValidCapabilityForm();

        model.Latitude = 4.7;
        AssertHasError(await validator.ValidateAsync(model), nameof(model.Longitude));

        model.Longitude = -181;
        AssertHasError(await validator.ValidateAsync(model), nameof(model.Longitude));

        model.Latitude = 91;
        model.Longitude = -74;
        AssertHasError(await validator.ValidateAsync(model), nameof(model.Latitude));
    }

    [TestMethod]
    public void ConcreteForms_ExposeOnlyTheirComposableCapabilities()
    {
        AssertCommonContactCapabilities(new CollectionPointForm());
        AssertCommonContactCapabilities(new ShelterForm());
        AssertCommonContactCapabilities(new HelpRequestForm());
        AssertCommonContactCapabilities(new BloodDonationCenterForm());

        Assert.IsTrue(new MissingPersonForm() is ITurnstileProtectedForm and IPrivacyConsentForm);
        Assert.IsFalse((object)new MissingPersonForm() is IFormWithLocation);
        Assert.IsTrue(new MissingPersonLocationForm() is IFormWithLocation);
        Assert.IsTrue(new DamageReportForm() is IPrivacyConsentForm and IFormWithLocation);
        Assert.IsFalse((object)new DamageReportForm() is ITurnstileProtectedForm);
    }

    [TestMethod]
    public async Task ResourceValidators_EnforceRequiredTextAndMaximumLengths()
    {
        var collection = ValidCollectionPoint();
        collection.Name = new string('a', 201);
        AssertHasError(await new CollectionPointFormValidator().ValidateAsync(collection), nameof(collection.Name));

        var shelter = ValidShelter();
        shelter.Description = string.Empty;
        AssertHasError(await new ShelterFormValidator().ValidateAsync(shelter), nameof(shelter.Description));
    }

    [TestMethod]
    public async Task HelpRequest_RequiresPhoneOrWhatsAppAndValidFlags()
    {
        var model = ValidHelpRequest();
        model.Phone = null;
        model.WhatsApp = null;
        AssertHasError(await new HelpRequestFormValidator().ValidateAsync(model), nameof(model.Phone));

        model.WhatsApp = "+57 300 123 4567";
        Assert.IsTrue((await new HelpRequestFormValidator().ValidateAsync(model)).IsValid);

        model.NeedCategories = HelpNeedCategory.None;
        AssertHasError(await new HelpRequestFormValidator().ValidateAsync(model), nameof(model.NeedCategories));
    }

    [TestMethod]
    public async Task BloodDonation_ValidatesFlagsAndTemporaryCampaignDates()
    {
        var model = ValidBloodDonation();
        model.CenterType = BloodDonationCenterType.TemporaryCampaign;
        model.StartsAt = null;
        model.EndsAt = null;
        var missingDates = await new BloodDonationCenterFormValidator().ValidateAsync(model);
        AssertHasError(missingDates, nameof(model.StartsAt));
        AssertHasError(missingDates, nameof(model.EndsAt));

        model.StartsAt = new DateTime(2026, 8, 20);
        model.EndsAt = new DateTime(2026, 8, 19);
        AssertHasError(await new BloodDonationCenterFormValidator().ValidateAsync(model), nameof(model.EndsAt));

        model.EndsAt = new DateTime(2026, 8, 21);
        model.BloodTypes = BloodTypeFlags.None;
        AssertHasError(await new BloodDonationCenterFormValidator().ValidateAsync(model), nameof(model.BloodTypes));
    }

    [TestMethod]
    public async Task MissingPerson_ValidatesDocumentLocationAndPhotoDependencies()
    {
        var model = ValidMissingPerson();
        model.IdentificationNumber = "123456";
        model.IdentificationDocumentType = null;
        AssertHasError(await new MissingPersonFormValidator().ValidateAsync(model), nameof(model.IdentificationDocumentType));

        model.IdentificationNumber = null;
        model.Locations[0].Address = string.Empty;
        Assert.IsFalse((await new MissingPersonFormValidator().ValidateAsync(model)).IsValid);

        model.Locations[0].Address = "Calle 1";
        model.Photo = new FakeBrowserFile("photo.gif", "image/gif", 100);
        AssertHasError(await new MissingPersonFormValidator().ValidateAsync(model), nameof(model.Photo));

        model.Photo = new FakeBrowserFile("photo.jpg", "image/jpeg", 10 * 1024 * 1024 + 1);
        AssertHasError(await new MissingPersonFormValidator().ValidateAsync(model), nameof(model.Photo));
    }

    [TestMethod]
    public async Task DamageReport_RequiresCoordinatesAndValidatesUploads()
    {
        var model = new DamageReportForm { Description = "Daño", PrivacyConsent = true };
        var validator = new DamageReportFormValidator();
        var missingLocation = await validator.ValidateAsync(model);
        AssertHasError(missingLocation, nameof(model.Latitude));
        AssertHasError(missingLocation, nameof(model.Longitude));

        model.Latitude = 4.7;
        model.Longitude = -74.1;
        model.MediaFiles.Add(new FakeBrowserFile("file.pdf", "application/pdf", 100));
        Assert.IsFalse((await validator.ValidateAsync(model)).IsValid);

        model.MediaFiles.Clear();
        for (var index = 0; index <= DamageReportFormValidator.MaxFileCount; index++)
            model.MediaFiles.Add(new FakeBrowserFile($"{index}.jpg", "image/jpeg", 100));
        AssertHasError(await validator.ValidateAsync(model), nameof(model.MediaFiles));
    }

    [TestMethod]
    public void CollectionPointMapping_TrimsNormalizesAndConvertsUtc()
    {
        var model = ValidCollectionPoint();
        model.Name = "  Centro  ";
        model.OrganizationName = "   ";
        model.EndsAt = new DateTime(2026, 8, 20, 15, 30, 0);

        var dto = model.ToApiDto();

        Assert.AreEqual("Centro", dto.Name);
        Assert.IsNull(dto.OrganizationName);
        Assert.AreEqual(TimeSpan.Zero, dto.EndsAt!.Value.Offset);
        Assert.AreEqual(model.PrivacyConsent, dto.PrivacyConsent);
    }

    [TestMethod]
    public void ShelterMapping_MapsContactAndCoordinates()
    {
        var model = ValidShelter();
        model.Phone = "  +57 300 123 4567  ";
        model.Latitude = 4.6;
        model.Longitude = -74.1;

        var dto = model.ToApiDto();

        Assert.AreEqual("+57 300 123 4567", dto.ContactPhone);
        Assert.AreEqual(4.6, dto.Latitude);
        Assert.AreEqual(-74.1, dto.Longitude);
    }

    [TestMethod]
    public void HelpRequestMapping_MapsPublicConsentFlagsAndUtcDate()
    {
        var model = ValidHelpRequest();
        model.Phone = null;
        model.WhatsApp = "  +57 300 123 4567 ";
        model.NeededBy = new DateTime(2026, 8, 20);

        var dto = model.ToApiDto();

        Assert.AreEqual(string.Empty, dto.PublicPhone);
        Assert.AreEqual("+57 300 123 4567", dto.PublicWhatsApp);
        Assert.IsTrue(dto.PublicContactConsent);
        Assert.AreEqual(TimeSpan.Zero, dto.NeededBy!.Value.Offset);
    }

    [TestMethod]
    public void BloodDonationMapping_MapsFlagsAndCampaignDates()
    {
        var model = ValidBloodDonation();
        model.StartsAt = new DateTime(2026, 8, 20);
        model.EndsAt = new DateTime(2026, 8, 21);

        var dto = model.ToApiDto();

        Assert.AreEqual(model.BloodTypes, dto.BloodTypes);
        Assert.AreEqual(model.Components, dto.Components);
        Assert.AreEqual(TimeSpan.Zero, dto.StartsAt!.Value.Offset);
        Assert.AreEqual(TimeSpan.Zero, dto.EndsAt!.Value.Offset);
    }

    [TestMethod]
    public void MissingPersonMapping_MapsPublicationConsentAndNestedLocations()
    {
        var model = ValidMissingPerson();
        model.Locations[0].Address = "  Calle 1  ";
        model.Locations[0].Note = "   ";

        var dto = model.ToApiDto();

        Assert.IsTrue(dto.PublicationConsent);
        Assert.AreEqual("Calle 1", dto.Locations[0].Address);
        Assert.IsNull(dto.Locations[0].Note);
        Assert.AreEqual(TimeSpan.Zero, dto.LastSeenAt.Offset);
    }

    [TestMethod]
    public void DamageReportMapping_MapsConsentLocationAndFlags()
    {
        var model = new DamageReportForm
        {
            Description = "  Grietas visibles  ",
            Latitude = 4.7,
            Longitude = -74.1,
            DamageSigns = DamageSign.Cracks | DamageSign.FallenDebris,
            PrivacyConsent = true,
        };

        var dto = model.ToApiDto();

        Assert.AreEqual("Grietas visibles", dto.Description);
        Assert.AreEqual(model.DamageSigns, dto.DamageSigns);
        Assert.IsTrue(dto.PrivacyConsent);
    }

    private static CapabilityForm ValidCapabilityForm() => new()
    {
        Address = "Calle 1",
        PrivacyConsent = true,
        TurnstileToken = "token",
    };

    private static CollectionPointForm ValidCollectionPoint() => new()
    {
        Name = "Centro",
        Address = "Calle 1",
        NeedsSummary = "Agua",
        ReceivingInstructions = "De 8 a 5",
        PrivacyConsent = true,
        TurnstileToken = "token",
    };

    private static ShelterForm ValidShelter() => new()
    {
        Name = "Refugio",
        Address = "Calle 1",
        Description = "Descripción",
        OperatingInstructions = "Abierto",
        PrivacyConsent = true,
        TurnstileToken = "token",
    };

    private static HelpRequestForm ValidHelpRequest() => new()
    {
        Title = "Ayuda",
        RequesterName = "Equipo",
        Address = "Calle 1",
        NeedDetails = "Agua",
        Phone = "+57 300 123 4567",
        Priority = HelpRequestPriority.Medium,
        NeedCategories = HelpNeedCategory.Other,
        PrivacyConsent = true,
        TurnstileToken = "token",
    };

    private static BloodDonationCenterForm ValidBloodDonation() => new()
    {
        Name = "Banco",
        Address = "Calle 1",
        OperatingInstructions = "De 8 a 5",
        NeedsSummary = "O+",
        Phone = "+57 300 123 4567",
        BloodTypes = BloodTypeFlags.OPositive,
        Components = BloodComponentFlags.WholeBlood,
        PrivacyConsent = true,
        TurnstileToken = "token",
    };

    private static MissingPersonForm ValidMissingPerson()
    {
        var model = new MissingPersonForm
        {
            FullName = "Persona",
            Description = "Descripción",
            LastSeenAt = new DateTime(2026, 8, 12),
            PrivacyConsent = true,
            TurnstileToken = "token",
        };
        model.Locations[0].Address = "Calle 1";
        return model;
    }

    private static void AssertCommonContactCapabilities(object model)
    {
        Assert.IsTrue(model is ITurnstileProtectedForm);
        Assert.IsTrue(model is IPrivacyConsentForm);
        Assert.IsTrue(model is IFormWithLocation);
        Assert.IsTrue(model is IFormWithOrganization);
        Assert.IsTrue(model is IFormWithEmail);
        Assert.IsTrue(model is IFormWithPhone);
        Assert.IsTrue(model is IFormWithWhatsApp);
    }

    private static void AssertHasError(FluentValidation.Results.ValidationResult result, string propertyName) =>
        Assert.IsTrue(result.Errors.Any(error =>
            error.PropertyName == propertyName || error.PropertyName.StartsWith($"{propertyName}[", StringComparison.Ordinal)));

    private sealed class CapabilityForm : ITurnstileProtectedForm, IPrivacyConsentForm, IFormWithLocation,
        IFormWithEmail, IFormWithPhone, IFormWithWhatsApp, IFormWithOrganization
    {
        public string TurnstileToken { get; set; } = string.Empty;
        public bool PrivacyConsent { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? WhatsApp { get; set; }
        public string? OrganizationName { get; set; }
    }

    private sealed class CapabilityFormValidator : MudFormValidator<CapabilityForm>
    {
        public CapabilityFormValidator()
        {
            this.AddTurnstileRules();
            this.AddPrivacyConsentRules();
            this.AddLocationRules(400);
            this.AddEmailRules();
            this.AddPhoneRules();
            this.AddWhatsAppRules();
            this.AddOrganizationRules();
        }
    }

    private sealed class FakeBrowserFile(string name, string contentType, long size) : IBrowserFile
    {
        public string Name { get; } = name;
        public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;
        public long Size { get; } = size;
        public string ContentType { get; } = contentType;
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream();
    }
}
