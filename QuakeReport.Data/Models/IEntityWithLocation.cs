using NetTopologySuite.Geometries;
using StorageGenerics.Core.Contracts;

namespace QuakeReport.Data.Models;

public interface IEntityWithLocation : IEntity<Guid>
{
    Point? Location { get; set; }
}

public interface IGeocodableEntity : IEntityWithLocation
{
    string? Address { get; }
}
