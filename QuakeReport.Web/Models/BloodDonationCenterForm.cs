using FluentValidation;
using QuakeReport.Contracts.Enums;

namespace QuakeReport.Web.Models;

public sealed class BloodDonationCenterForm : ITurnstileProtectedForm, IPrivacyConsentForm, IFormWithLocation,
    IFormWithEmail, IFormWithPhone, IFormWithWhatsApp, IFormWithOrganization
{
    public string Name { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Description { get; set; }
    public string OperatingInstructions { get; set; } = string.Empty;
    public string NeedsSummary { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Email { get; set; }
    public BloodDonationCenterType CenterType { get; set; } = BloodDonationCenterType.PermanentSite;
    public BloodTypeFlags BloodTypes { get; set; }
    public BloodComponentFlags Components { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool PrivacyConsent { get; set; }
    public string TurnstileToken { get; set; } = string.Empty;
}

public sealed class BloodDonationCenterFormValidator : MudFormValidator<BloodDonationCenterForm>
{
    private const BloodTypeFlags AllBloodTypes = BloodTypeFlags.APositive | BloodTypeFlags.ANegative |
        BloodTypeFlags.BPositive | BloodTypeFlags.BNegative | BloodTypeFlags.ABPositive |
        BloodTypeFlags.ABNegative | BloodTypeFlags.OPositive | BloodTypeFlags.ONegative | BloodTypeFlags.Unknown;
    private const BloodComponentFlags AllComponents = BloodComponentFlags.WholeBlood |
        BloodComponentFlags.RedBloodCells | BloodComponentFlags.Plasma | BloodComponentFlags.Platelets | BloodComponentFlags.Unknown;

    public BloodDonationCenterFormValidator()
    {
        this.AddTurnstileRules();
        this.AddPrivacyConsentRules();
        this.AddLocationRules(400);
        this.AddOrganizationRules();
        this.AddEmailRules();
        this.AddPhoneRules();
        this.AddWhatsAppRules(); 

        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(200).WithMessage("El nombre no puede superar 200 caracteres.");
        RuleFor(x => x.Description).MaximumLength(3000).WithMessage("La descripción no puede superar 3000 caracteres.");
        RuleFor(x => x.OperatingInstructions).NotEmpty().WithMessage("Las instrucciones son obligatorias.").MaximumLength(2500).WithMessage("Las instrucciones no pueden superar 2500 caracteres.");

        RuleFor(x => x.NeedsSummary).MaximumLength(2000).WithMessage("El resumen no puede superar 2000 caracteres.");

        RuleFor(x => x.CenterType).IsInEnum().WithMessage("Selecciona un tipo de centro válido.");
        RuleFor(x => x.BloodTypes).Must(value => value != BloodTypeFlags.None && (value & ~AllBloodTypes) == 0).WithMessage("Selecciona al menos un grupo sanguíneo.");
        RuleFor(x => x.Components).Must(value => value != BloodComponentFlags.None && (value & ~AllComponents) == 0).WithMessage("Selecciona al menos un componente.");
        RuleFor(x => x.StartsAt).NotNull().When(x => x.CenterType == BloodDonationCenterType.TemporaryCampaign).WithMessage("Indica la fecha de inicio de la campaña.");
        RuleFor(x => x.EndsAt).NotNull().When(x => x.CenterType == BloodDonationCenterType.TemporaryCampaign).WithMessage("Indica la fecha de fin de la campaña.");
        RuleFor(x => x.EndsAt).Must((form, end) => !end.HasValue || !form.StartsAt.HasValue || end.Value >= form.StartsAt.Value).WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}
