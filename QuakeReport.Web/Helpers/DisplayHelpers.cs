using System.Text.RegularExpressions;
using MudBlazor;
using QuakeReport.Contracts.Enums;

namespace QuakeReport.Web.Helpers;

public static class DisplayHelpers
{
    /// <summary>Inserts spaces into a PascalCase enum name for display, e.g. "ApartmentComplex" -> "Apartment Complex".</summary>
    public static string Humanize(this Enum value) =>
        Regex.Replace(value.ToString(), "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");

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
        $"https://maps.google.com/maps?q={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&z=15&output=embed";

    public static string GoogleMapsLinkUrl(double latitude, double longitude) =>
        $"https://www.google.com/maps?q={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}
