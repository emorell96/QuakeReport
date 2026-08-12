using System.ComponentModel.DataAnnotations;
using QuakeReport.Contracts.Enums;

namespace QuakeReport.Contracts.Dtos;

public class CreateDamageReportRequest
{
    [Required]
    [MaxLength(2000)]
    public required string Description { get; set; }

    [Required]
    public required SeverityLevel Severity { get; set; }

    public DamageSign DamageSigns { get; set; } = DamageSign.None;

    public StructureType? StructureType { get; set; }

    public StructureSize? StructureSize { get; set; }

    [Range(-90, 90)]
    public required double Latitude { get; set; }

    [Range(-180, 180)]
    public required double Longitude { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    public bool PrivacyConsent { get; set; }
}
