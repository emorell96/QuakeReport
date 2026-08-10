using System.Globalization;
using MudBlazor;
using QuakeReport.Contracts.Enums;

namespace QuakeReport.Web.Helpers;

/// <summary>Spanish display text for enums and other UI formatting helpers. Site is Spanish-only, no locale switcher.</summary>
public static class DisplayHelpers
{
    public static string ToDisplayString(this SeverityLevel severity) => severity switch
    {
        SeverityLevel.Minor => "Menor",
        SeverityLevel.Moderate => "Moderado",
        SeverityLevel.Major => "Mayor",
        SeverityLevel.Severe => "Severo",
        SeverityLevel.Catastrophic => "Catastrófico",
        _ => severity.ToString(),
    };

    public static string ToDisplayString(this DamageSign sign) => sign switch
    {
        DamageSign.Cracks => "Grietas",
        DamageSign.PartialCollapse => "Colapso parcial",
        DamageSign.FullCollapse => "Colapso total",
        DamageSign.FallenDebris => "Escombros caídos",
        DamageSign.FireOrSmoke => "Fuego o humo",
        DamageSign.GasSmell => "Olor a gas",
        DamageSign.WaterLeakOrFlooding => "Fuga de agua o inundación",
        DamageSign.DownedPowerLines => "Cables eléctricos caídos",
        DamageSign.BlockedRoad => "Vía bloqueada",
        DamageSign.LandslideOrRockfall => "Deslizamiento de tierra o rocas",
        DamageSign.PeopleTrapped => "Personas atrapadas",
        _ => sign.ToString(),
    };

    public static string ToDisplayString(this StructureType type) => type switch
    {
        StructureType.House => "Casa",
        StructureType.Apartment => "Apartamento",
        StructureType.ApartmentComplex => "Conjunto de apartamentos",
        StructureType.Commercial => "Comercial",
        StructureType.Other => "Otro",
        _ => type.ToString(),
    };

    public static string ToDisplayString(this StructureSize size) => size switch
    {
        StructureSize.Small => "Pequeño",
        StructureSize.Medium => "Mediano",
        StructureSize.Large => "Grande",
        _ => size.ToString(),
    };

    public static Color SeverityColor(SeverityLevel severity) => severity switch
    {
        SeverityLevel.Minor => Color.Success,
        SeverityLevel.Moderate => Color.Info,
        SeverityLevel.Major => Color.Warning,
        SeverityLevel.Severe => Color.Warning,
        SeverityLevel.Catastrophic => Color.Error,
        _ => Color.Default,
    };

    /// <summary>Splits a [Flags] DamageSign value into its individual set flags, in declaration order.</summary>
    public static IEnumerable<DamageSign> Split(this DamageSign signs)
    {
        foreach (DamageSign flag in Enum.GetValues<DamageSign>())
        {
            if (flag != DamageSign.None && signs.HasFlag(flag))
            {
                yield return flag;
            }
        }
    }

    public static string GoogleMapsEmbedUrl(double latitude, double longitude) =>
        $"https://maps.google.com/maps?q={latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}&z=15&output=embed";

    public static string GoogleMapsLinkUrl(double latitude, double longitude) =>
        $"https://www.google.com/maps?q={latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}";
}
