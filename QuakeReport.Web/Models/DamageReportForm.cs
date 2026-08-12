using FluentValidation;
using Microsoft.AspNetCore.Components.Forms;
using QuakeReport.Contracts.Enums;

namespace QuakeReport.Web.Models;

public sealed class DamageReportForm : IPrivacyConsentForm, IFormWithLocation
{
    public string Description { get; set; } = string.Empty;
    public SeverityLevel Severity { get; set; } = SeverityLevel.Moderate;
    public DamageSign DamageSigns { get; set; }
    public StructureType? StructureType { get; set; }
    public StructureSize? StructureSize { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool PrivacyConsent { get; set; }
    public List<IBrowserFile> MediaFiles { get; } = [];
}

public sealed class DamageReportFormValidator : MudFormValidator<DamageReportForm>
{
    public const long MaxFileSizeBytes = 50 * 1024 * 1024;
    public const int MaxFileCount = 20;
    private const DamageSign AllDamageSigns = DamageSign.Cracks | DamageSign.PartialCollapse |
        DamageSign.FullCollapse | DamageSign.FallenDebris | DamageSign.FireOrSmoke | DamageSign.GasSmell |
        DamageSign.WaterLeakOrFlooding | DamageSign.DownedPowerLines | DamageSign.BlockedRoad |
        DamageSign.LandslideOrRockfall | DamageSign.PeopleTrapped;

    public DamageReportFormValidator()
    {
        this.AddPrivacyConsentRules(); this.AddLocationRules(300, addressRequired: false, coordinatesRequired: true);
        RuleFor(x => x.Description).NotEmpty().WithMessage("La descripción es obligatoria.").MaximumLength(2000).WithMessage("La descripción no puede superar 2000 caracteres.");
        RuleFor(x => x.Severity).IsInEnum().WithMessage("Selecciona una gravedad válida.");
        RuleFor(x => x.DamageSigns).Must(value => (value & ~AllDamageSigns) == 0).WithMessage("La selección de daños no es válida.");
        RuleFor(x => x.StructureType).IsInEnum().When(x => x.StructureType.HasValue).WithMessage("Selecciona un tipo de estructura válido.");
        RuleFor(x => x.StructureSize).IsInEnum().When(x => x.StructureSize.HasValue).WithMessage("Selecciona un tamaño de estructura válido.");
        RuleFor(x => x.MediaFiles).Must(files => files.Count <= MaxFileCount).WithMessage("Puedes adjuntar máximo 20 archivos.");
        RuleForEach(x => x.MediaFiles).Must(file => file.Size <= MaxFileSizeBytes).WithMessage("Cada archivo debe pesar máximo 50 MB.")
            .Must(file => file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)).WithMessage("Solo se permiten imágenes y videos.");
    }
}
