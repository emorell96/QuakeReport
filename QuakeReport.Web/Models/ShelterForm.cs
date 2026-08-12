using FluentValidation;

namespace QuakeReport.Web.Models;

public sealed class ShelterForm : ITurnstileProtectedForm, IPrivacyConsentForm, IFormWithLocation,
    IFormWithEmail, IFormWithPhone, IFormWithWhatsApp, IFormWithOrganization
{
    public string Name { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Description { get; set; } = string.Empty;
    public string OperatingInstructions { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Email { get; set; }
    public bool PrivacyConsent { get; set; }
    public string TurnstileToken { get; set; } = string.Empty;
}

public sealed class ShelterFormValidator : MudFormValidator<ShelterForm>
{
    public ShelterFormValidator()
    {
        this.AddTurnstileRules(); this.AddPrivacyConsentRules(); this.AddLocationRules(400);
        this.AddOrganizationRules(); this.AddEmailRules(); this.AddPhoneRules(); this.AddWhatsAppRules();
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(200).WithMessage("El nombre no puede superar 200 caracteres.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("La descripción es obligatoria.").MaximumLength(3000).WithMessage("La descripción no puede superar 3000 caracteres.");
        RuleFor(x => x.OperatingInstructions).NotEmpty().WithMessage("Las instrucciones de funcionamiento son obligatorias.").MaximumLength(2000).WithMessage("Las instrucciones no pueden superar 2000 caracteres.");
        RuleFor(x => x.ContactName).MaximumLength(200).WithMessage("El contacto no puede superar 200 caracteres.");
    }
}
