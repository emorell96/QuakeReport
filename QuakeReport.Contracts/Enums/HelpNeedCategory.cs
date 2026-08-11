namespace QuakeReport.Contracts.Enums;

[Flags]
public enum HelpNeedCategory
{
    None = 0,
    Personnel = 1,
    Medical = 2,
    RescueEquipment = 4,
    Machinery = 8,
    Transportation = 16,
    FoodAndWater = 32,
    Communications = 64,
    TemporaryShelter = 128,
    Security = 256,
    Other = 512,
}
