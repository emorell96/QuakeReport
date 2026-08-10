namespace QuakeReport.Data.Models;

public class MissingPersonTip
{
    public Guid Id { get; set; }
    public required Guid MissingPersonId { get; set; }
    public MissingPerson? MissingPerson { get; set; }
    public required string Message { get; set; }
    public DateTimeOffset? SightedAt { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ResponderName { get; set; }
    public string? ResponderPhone { get; set; }
    public string? ResponderEmail { get; set; }
    public bool IsHidden { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
