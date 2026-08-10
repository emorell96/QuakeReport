namespace QuakeReport.Data.Models;

/// <summary>
/// Plain-language signs of damage a reporter can actually observe and check
/// off themselves - not an engineering classification. Flags so a single
/// report can capture more than one (e.g. Cracks | GasSmell).
/// </summary>
[Flags]
public enum DamageSign
{
    None = 0,

    /// <summary>Visible cracks in walls, floors, or ceilings.</summary>
    Cracks = 1 << 0,

    /// <summary>Part of a structure fell - a wall, roof, or balcony.</summary>
    PartialCollapse = 1 << 1,

    /// <summary>The building came down.</summary>
    FullCollapse = 1 << 2,

    /// <summary>Rubble, fallen objects, or fallen trees blocking access.</summary>
    FallenDebris = 1 << 3,

    FireOrSmoke = 1 << 4,

    /// <summary>Possible gas leak.</summary>
    GasSmell = 1 << 5,

    /// <summary>Burst pipes or flooding.</summary>
    WaterLeakOrFlooding = 1 << 6,

    DownedPowerLines = 1 << 7,

    BlockedRoad = 1 << 8,

    LandslideOrRockfall = 1 << 9,

    /// <summary>Highest-priority signal - someone is trapped or hurt.</summary>
    PeopleTrapped = 1 << 10
}
