using StorageGenerics.Core.Contracts;

namespace QuakeReport.Data.Models;

public class HelpRequestAbuseReport : IEntity<Guid>
{
    public Guid Id { get; set; }
    public required Guid HelpRequestId { get; set; }
    public HelpRequest? HelpRequest { get; set; }
    public required string Reason { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
