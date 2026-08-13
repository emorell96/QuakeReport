using StorageGenerics.Core.Contracts;

namespace QuakeReport.Data.Models;

public class CollectionPointComment : IEntity<Guid>
{
    public Guid Id { get; set; }
    public required Guid CollectionPointId { get; set; }
    public CollectionPoint? CollectionPoint { get; set; }
    public string? DisplayName { get; set; }
    public required string Message { get; set; }
    public bool IsHidden { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
