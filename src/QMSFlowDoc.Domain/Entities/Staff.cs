using QMSFlowDoc.Domain.Identity;
using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Domain.Entities;

public enum CompetencyOutcome
{
    PASS,
    FAIL,
    CONDITIONAL
}

public class StaffProfile
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string? PositionTitle { get; set; }
    public string? Department { get; set; }
    public DateTime? HiredAt { get; set; }
    public bool IsActive { get; set; } = true;

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<StaffTraining> Trainings { get; set; } = new();
    public List<CompetencyEvaluation> CompetencyEvaluations { get; set; } = new();
    public List<StaffCompetencyStatus> CompetencyStatuses { get; set; } = new();
    public List<StaffAuthorization> Authorizations { get; set; } = new();
}
