using NetTopologySuite.Geometries;

namespace QuakeReport.Data.Models;

public class MissingPersonLocation : IGeocodableEntity
{
    public Guid Id { get; set; }
    public required Guid MissingPersonId { get; set; }
    public MissingPerson? MissingPerson { get; set; }
    public required string Address { get; set; }
    public string? SearchAddress { get; set; }
    public Point? Location { get; set; }
    public string? Note { get; set; }
}
