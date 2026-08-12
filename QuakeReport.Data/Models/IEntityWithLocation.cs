using NetTopologySuite.Geometries;

namespace QuakeReport.Data.Models;

public interface IEntityWithLocation
{
    Guid Id { get; }
    Point? Location { get; set; }
}

public interface IGeocodableEntity : IEntityWithLocation
{
    string? Address { get; }
}
