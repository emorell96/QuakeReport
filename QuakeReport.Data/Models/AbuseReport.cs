using StorageGenerics.Core.Contracts;

namespace QuakeReport.Data.Models;

public class AbuseReport : IEntity<Guid>
{
    public Guid Id { get; set; }
    public required Guid MissingPersonId { get; set; }
    public MissingPerson? MissingPerson { get; set; }
    public Guid? TipId { get; set; }
    public string? Reason { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
