using FluentValidation;

namespace QuakeReport.Web.Models;

public sealed class CollectionPointForm : ITurnstileProtectedForm, IPrivacyConsentForm, IFormWithLocation,
    IFormWithEmail, IFormWithPhone, IFormWithWhatsApp, IFormWithOrganization
{
    public string Name { get;
        set;
        } = string.Empty;
    public string? OrganizationName { get;
        set;
        }
    public string? Address { get;
        set;
        }
    public double? Latitude { get;
        set;
        }
    public double? Longitude { get;
        set;
        }
    public string NeedsSummary { get;
        set;
        } = string.Empty;
    public string ReceivingInstructions { get;
        set;
        } = string.Empty;
    public string? Description { get;
        set;
        }
    public string? ContactName { get;
        set;
        }
    public string? Phone { get;
        set;
        }
    public string? WhatsApp { get;
        set;
        }
    public string? Email { get;
        set;
        }
    public DateTime? EndsAt { get;
        set;
        }
    public bool PrivacyConsent { get;
        set;
        }
    public string TurnstileToken { get;
        set;
        } = string.Empty;
}

public sealed class CollectionPointFormValidator : MudFormValidator<CollectionPointForm>
{
    public CollectionPointFormValidator()
    {
        this.AddTurnstileRules();
        this.AddPrivacyConsentRules();
        this.AddLocationRules(400);
        this.AddOrganizationRules();
        this.AddEmailRules();
        this.AddPhoneRules();
        this.AddWhatsAppRules();
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(200).WithMessage("El nombre no puede superar 200 caracteres.");
        RuleFor(x => x.NeedsSummary).NotEmpty().WithMessage("Indica qué se necesita actualmente.").MaximumLength(2000).WithMessage("Las necesidades no pueden superar 2000 caracteres.");
        RuleFor(x => x.ReceivingInstructions).NotEmpty().WithMessage("Las instrucciones de recepción son obligatorias.").MaximumLength(2000).WithMessage("Las instrucciones no pueden superar 2000 caracteres.");
        RuleFor(x => x.Description).MaximumLength(3000).WithMessage("La descripción no puede superar 3000 caracteres.");
        RuleFor(x => x.ContactName).MaximumLength(200).WithMessage("El contacto no puede superar 200 caracteres.");
        RuleFor(x => x.EndsAt).Must(value => !value.HasValue || value.Value >= DateTime.Now.AddMinutes(-5)).WithMessage("La fecha de cierre no puede estar en el pasado.");
    }
}
