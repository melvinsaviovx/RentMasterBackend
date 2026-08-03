using RentMaster.Domain.Common;

namespace RentMaster.Domain.Entities;

public sealed class PropertyPhoto : BaseEntity
{
    public Guid PropertyId { get; set; }
    public required string StorageObjectName { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public int SortOrder { get; set; }

    public Property Property { get; set; } = null!;
}
