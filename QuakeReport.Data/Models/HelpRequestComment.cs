namespace QuakeReport.Data.Models;

public class HelpRequestComment
{
    public Guid Id { get; set; }
    public required Guid HelpRequestId { get; set; }
    public HelpRequest? HelpRequest { get; set; }
    public string? DisplayName { get; set; }
    public required string Message { get; set; }
    public bool IsHidden { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
