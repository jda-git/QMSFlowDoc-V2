using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Domain.Entities;

public enum MethodStatus
{
    DRAFT,
    ACTIVE,
    OBSOLETE
}

public class Method
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public MethodStatus Status { get; set; } = MethodStatus.DRAFT;
    public string? CurrentVersion { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public Guid? DocumentId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<MethodAuthorization> Authorizations { get; set; } = new();
}

public class MethodAuthorization
{
    public Guid Id { get; set; }
    public Guid MethodId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime AuthorizedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? AuthorizedByUserId { get; set; }
    public string? AuthorizedByName { get; set; }
}

public class MethodReagent
{
    public Guid Id { get; set; }
    public Guid MethodId { get; set; }
    public Guid ReagentId { get; set; }
    public string? ReagentName { get; set; }
}

// === Incertidumbre de Medida (ISO 15189 §6.5, 7.3.4) ===

public class MeasurementUncertainty
{
    public Guid Id { get; set; }
    public Guid MethodId { get; set; }
    public string AnalyteName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double CoverageFactor { get; set; } = 2.0;
    public string ConfidenceLevel { get; set; } = "95%";
    public DateTime EstimatedDate { get; set; }
    public string? Notes { get; set; }
}

public class MethodVersion
{
    public Guid Id { get; set; }
    public Guid MethodId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT"; // DRAFT, APPROVED, OBSOLETE
    public string? ChangeDescription { get; set; }
    public string? DocumentPath { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class MethodValidation
{
    public Guid Id { get; set; }
    public Guid MethodVersionId { get; set; }
    public string Parameter { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public int ExperimentCount { get; set; }
    public string? ReportPath { get; set; }
    public string? Notes { get; set; }
}
