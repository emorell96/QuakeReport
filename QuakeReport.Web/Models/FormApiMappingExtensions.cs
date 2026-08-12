using QuakeReport.Contracts.Dtos;

namespace QuakeReport.Web.Models;

public static class FormApiMappingExtensions
{
    public static CreateCollectionPointRequest ToApiDto(this CollectionPointForm form) => new(
        Required(form.Name), Optional(form.OrganizationName), Required(form.Address), form.Latitude, form.Longitude,
        Optional(form.Description), Required(form.NeedsSummary), Required(form.ReceivingInstructions), Optional(form.ContactName),
        Optional(form.Phone), Optional(form.WhatsApp), Optional(form.Email), ToUtc(form.EndsAt), form.PrivacyConsent, Required(form.TurnstileToken));

    public static CreateShelterRequest ToApiDto(this ShelterForm form) => new(
        Required(form.Name), Optional(form.OrganizationName), Required(form.Address), form.Latitude, form.Longitude,
        Required(form.Description), Required(form.OperatingInstructions), Optional(form.ContactName), Optional(form.Phone),
        Optional(form.WhatsApp), Optional(form.Email), form.PrivacyConsent, Required(form.TurnstileToken));

    public static CreateHelpRequestRequest ToApiDto(this HelpRequestForm form) => new(
        Required(form.Title), Required(form.RequesterName), Optional(form.OrganizationName), Required(form.Address),
        form.Latitude, form.Longitude, Required(form.NeedDetails), Optional(form.Instructions), Optional(form.Phone) ?? string.Empty,
        Optional(form.WhatsApp), Optional(form.Email), form.Priority, form.NeedCategories, ToUtc(form.NeededBy),
        form.PrivacyConsent, Required(form.TurnstileToken));

    public static CreateBloodDonationCenterRequest ToApiDto(this BloodDonationCenterForm form) => new(
        Required(form.Name), Optional(form.OrganizationName), Required(form.Address), form.Latitude, form.Longitude,
        Optional(form.Description), Required(form.OperatingInstructions), Required(form.NeedsSummary), Optional(form.Phone) ?? string.Empty,
        Optional(form.WhatsApp), Optional(form.Email), form.CenterType, form.BloodTypes, form.Components,
        ToUtc(form.StartsAt), ToUtc(form.EndsAt), form.PrivacyConsent, Required(form.TurnstileToken));

    public static CreateMissingPersonRequest ToApiDto(this MissingPersonForm form) => new(
        Required(form.FullName), Optional(form.Aliases), Optional(form.ApproximateAge), form.IdentificationDocumentType,
        Optional(form.IdentificationNumber), Required(form.Description), Optional(form.PhysicalDescription),
        Optional(form.ClothingDescription), ToUtc(form.LastSeenAt)!.Value,
        form.Locations.Select(location => location.ToApiDto()).ToList(), form.PrivacyConsent, Required(form.TurnstileToken));

    public static CreateMissingPersonLocationRequest ToApiDto(this MissingPersonLocationForm form) =>
        new(Required(form.Address), form.Latitude, form.Longitude, Optional(form.Note));

    public static CreateDamageReportRequest ToApiDto(this DamageReportForm form) => new()
    {
        Description = Required(form.Description), Severity = form.Severity, DamageSigns = form.DamageSigns,
        StructureType = form.StructureType, StructureSize = form.StructureSize,
        Latitude = form.Latitude!.Value, Longitude = form.Longitude!.Value,
        Address = Optional(form.Address), PrivacyConsent = form.PrivacyConsent,
    };

    private static string Required(string? value) => value?.Trim() ?? string.Empty;
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTimeOffset? ToUtc(DateTime? value) => value is null
        ? null
        : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified), TimeSpan.Zero);
}
