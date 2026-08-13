using QuakeReport.Contracts.Enums;

namespace QuakeReport.Web.Services;

public static class BloodDonationDisplay
{
    public static string Type(BloodDonationCenterType value) =>
        value == BloodDonationCenterType.PermanentSite ? "Sede permanente" : "Campaña temporal";

    public static string Status(BloodDonationOperationalStatus value) => value switch
    {
        BloodDonationOperationalStatus.Open => "Abierto",
        BloodDonationOperationalStatus.TemporarilyUnavailable => "No disponible temporalmente",
        _ => "Cerrado"
    };

    public static string BloodTypes(BloodTypeFlags value)
    {
        var names = new[]
        {
            (BloodTypeFlags.APositive, "A+"),
            (BloodTypeFlags.ANegative, "A−"),
            (BloodTypeFlags.BPositive, "B+"),
            (BloodTypeFlags.BNegative, "B−"),
            (BloodTypeFlags.ABPositive, "AB+"),
            (BloodTypeFlags.ABNegative, "AB−"),
            (BloodTypeFlags.OPositive, "O+"),
            (BloodTypeFlags.ONegative, "O−"),
            (BloodTypeFlags.Unknown, "No sé")
        };
        return string.Join(", ", names.Where(x => value.HasFlag(x.Item1)).Select(x => x.Item2));
    }

    public static string Components(BloodComponentFlags value)
    {
        var names = new[]
        {
            (BloodComponentFlags.WholeBlood, "Sangre total"),
            (BloodComponentFlags.RedBloodCells, "Glóbulos rojos"),
            (BloodComponentFlags.Plasma, "Plasma"),
            (BloodComponentFlags.Platelets, "Plaquetas"),
            (BloodComponentFlags.Unknown, "No sé")
        };
        return string.Join(", ", names.Where(x => value.HasFlag(x.Item1)).Select(x => x.Item2));
    }
}
