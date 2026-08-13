using FluentValidation;
using QuakeReport.Contracts.Enums;

namespace QuakeReport.Web.Models;

public sealed class HelpRequestForm : ITurnstileProtectedForm, IPrivacyConsentForm, IFormWithLocation,
    IFormWithEmail, IFormWithPhone, IFormWithWhatsApp, IFormWithOrganization
{
    public string Title { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string NeedDetails { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Email { get; set; }
    public HelpRequestPriority Priority { get; set; } = HelpRequestPriority.Medium;
    public HelpNeedCategory NeedCategories { get; set; } = HelpNeedCategory.Other;
    public DateTime? NeededBy { get; set; }
    public bool PrivacyConsent { get; set; }
    public string TurnstileToken { get; set; } = string.Empty;
}

public sealed class HelpRequestFormValidator : MudFormValidator<HelpRequestForm>
{
    private const HelpNeedCategory AllCategories = HelpNeedCategory.Personnel | HelpNeedCategory.Medical |
        HelpNeedCategory.RescueEquipment | HelpNeedCategory.Machinery | HelpNeedCategory.Transportation |
        HelpNeedCategory.FoodAndWater | HelpNeedCategory.Communications | HelpNeedCategory.TemporaryShelter |
        HelpNeedCategory.Security | HelpNeedCategory.Other;

    public HelpRequestFormValidator()
    {
        this.AddTurnstileRules();
        this.AddPrivacyConsentRules();
        this.AddLocationRules(400);
        this.AddOrganizationRules();
        this.AddEmailRules();
        this.AddPhoneRules();
        this.AddWhatsAppRules();
        this.RequirePhoneOrWhatsApp();
        RuleFor(x => x.Title).NotEmpty().WithMessage("El título es obligatorio.").MaximumLength(200).WithMessage("El título no puede superar 200 caracteres.");
        RuleFor(x => x.RequesterName).NotEmpty().WithMessage("Indica quién solicita la ayuda.").MaximumLength(200).WithMessage("El solicitante no puede superar 200 caracteres.");
        RuleFor(x => x.NeedDetails).NotEmpty().WithMessage("Describe la ayuda necesaria.").MaximumLength(3000).WithMessage("La descripción de la necesidad no puede superar 3000 caracteres.");
        RuleFor(x => x.Instructions).MaximumLength(2000).WithMessage("Las instrucciones no pueden superar 2000 caracteres.");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("Selecciona una prioridad válida.");
        RuleFor(x => x.NeedCategories).Must(value => value != HelpNeedCategory.None && (value & ~AllCategories) == 0).WithMessage("Selecciona al menos un tipo de ayuda válido.");
    }
}
