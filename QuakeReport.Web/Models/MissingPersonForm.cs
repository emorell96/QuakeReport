using FluentValidation;
using Microsoft.AspNetCore.Components.Forms;
using QuakeReport.Contracts.Enums;

namespace QuakeReport.Web.Models;

public sealed class MissingPersonForm : ITurnstileProtectedForm, IPrivacyConsentForm
{
    public string FullName { get; set; } = string.Empty;
    public string? Aliases { get; set; }
    public string? ApproximateAge { get; set; }
    public IdentificationDocumentType? IdentificationDocumentType { get; set; }
    public string? IdentificationNumber { get; set; }
    public DateTime? LastSeenAt { get; set; } = DateTime.Today;
    public string Description { get; set; } = string.Empty;
    public string? PhysicalDescription { get; set; }
    public string? ClothingDescription { get; set; }
    public List<MissingPersonLocationForm> Locations { get; } = [new()];
    public IBrowserFile? Photo { get; set; }
    public bool PrivacyConsent { get; set; }
    public string TurnstileToken { get; set; } = string.Empty;
}

public sealed class MissingPersonLocationForm : IFormWithLocation
{
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Note { get; set; }
}

public sealed class MissingPersonLocationFormValidator : MudFormValidator<MissingPersonLocationForm>
{
    public MissingPersonLocationFormValidator()
    {
        this.AddLocationRules(300);
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("La nota no puede superar 500 caracteres.");
    }
}

public sealed class MissingPersonFormValidator : MudFormValidator<MissingPersonForm>
{
    private const long MaxPhotoSize = 10 * 1024 * 1024;
    private static readonly string[] AllowedPhotoTypes = ["image/jpeg", "image/png", "image/webp"];

    public MissingPersonFormValidator()
    {
        this.AddTurnstileRules();
        this.AddPrivacyConsentRules();
        RuleFor(x => x.FullName).NotEmpty().WithMessage("El nombre completo es obligatorio.").MaximumLength(200).WithMessage("El nombre no puede superar 200 caracteres.");
        RuleFor(x => x.Aliases).MaximumLength(500).WithMessage("Los alias no pueden superar 500 caracteres.");
        RuleFor(x => x.ApproximateAge).MaximumLength(50).WithMessage("La edad aproximada no puede superar 50 caracteres.");
        RuleFor(x => x.IdentificationDocumentType).IsInEnum().When(x => x.IdentificationDocumentType.HasValue).WithMessage("Selecciona un tipo de documento válido.");
        RuleFor(x => x.IdentificationDocumentType).NotNull().When(x => !string.IsNullOrWhiteSpace(x.IdentificationNumber)).WithMessage("Selecciona el tipo de documento.");
        RuleFor(x => x.LastSeenAt).NotNull().WithMessage("Indica la fecha del último avistamiento.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("La descripción es obligatoria.").MaximumLength(2000).WithMessage("La descripción no puede superar 2000 caracteres.");
        RuleFor(x => x.PhysicalDescription).MaximumLength(1000).WithMessage("La descripción física no puede superar 1000 caracteres.");
        RuleFor(x => x.ClothingDescription).MaximumLength(1000).WithMessage("La ropa y detalles no pueden superar 1000 caracteres.");
        RuleFor(x => x.Locations).NotEmpty().WithMessage("Agrega al menos una ubicación.");
        RuleForEach(x => x.Locations).SetValidator(new MissingPersonLocationFormValidator());
        RuleFor(x => x.Photo).Must(file => file is null || file.Size <= MaxPhotoSize).WithMessage("La fotografía no puede superar 10 MB.");
        RuleFor(x => x.Photo).Must(file => file is null || AllowedPhotoTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase)).WithMessage("Selecciona una imagen JPEG, PNG o WebP.");
    }
}
