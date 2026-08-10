namespace QuakeReport.Data.Models;

public class CollectionPointAbuseReport
{
    public Guid Id { get; set; }
    public required Guid CollectionPointId { get; set; }
    public CollectionPoint? CollectionPoint { get; set; }
    public Guid? CommentId { get; set; }
    public required string Reason { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
