namespace QuakeReport.Data.Models;

public class ShelterAbuseReport
{
    public Guid Id { get; set; }
    public required Guid ShelterId { get; set; }
    public Shelter? Shelter { get; set; }
    public required string Reason { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
