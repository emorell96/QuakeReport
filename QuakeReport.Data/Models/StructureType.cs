namespace QuakeReport.Data.Models;

/// <summary>
/// The kind of structure a <see cref="DamageReport"/> is about. Optional -
/// not every report involves a structure (e.g. a blocked road, a landslide).
/// Combined with <see cref="Data.Models.StructureSize"/>, this is a rough,
/// reporter-eyeballed proxy for how many people are affected.
/// </summary>
public enum StructureType
{
    House,
    Apartment,
    ApartmentComplex,
    Commercial,
    Other
}
