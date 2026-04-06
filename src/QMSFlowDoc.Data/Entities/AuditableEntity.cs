namespace QMSFlowDoc.Data.Entities;

/// <summary>
/// Base class for all entities requiring audit trail and optimistic concurrency.
/// SQL Server uses rowversion/timestamp for RowVersion automatically.
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Optimistic concurrency token. Mapped to SQL Server rowversion.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
