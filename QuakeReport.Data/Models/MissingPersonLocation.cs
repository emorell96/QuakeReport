namespace QuakeReport.Data.Models;

public class MissingPersonLocation
{
    public Guid Id { get; set; }
    public required Guid MissingPersonId { get; set; }
    public MissingPerson? MissingPerson { get; set; }
    public required string Address { get; set; }
    public string? SearchAddress { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Note { get; set; }
}
