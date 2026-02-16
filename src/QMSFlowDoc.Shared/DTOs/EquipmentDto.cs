using System;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public class EquipmentListDto
{
    public Guid Id { get; set; }
    public string? InternalId { get; set; }
    public string? AssetTag { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? SoftwareVersion { get; set; } // ISP 15189
    public string? FirmwareVersion { get; set; } // ISP 15189
    public string? Location { get; set; }
    public EquipmentStatus Status { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? NextCalibration { get; set; }
    
    public Guid? LastMaintenanceEventId { get; set; }
    public DateTime? LastMaintenanceAt { get; set; }
    public string? LastEventType { get; set; }
    public string? LastOutcome { get; set; }
    public string? NextMaintenanceDue { get; set; }
    public string? TodayQCStatus { get; set; } // "PASS", "FAIL", "PENDING"
    public string? TodayQCColor { get; set; }

    public EquipmentListDto() { }
    // Constructor simplified or ignored as JSON deserialization usually handles it
    
    // Helper properties for UI binding
    public string LastMaintenanceDateFormatted => LastMaintenanceAt?.ToString("dd/MM/yyyy") ?? "-";
    public string StatusFormatted => Status.ToString();
    public string NextCalibrationFormatted => NextCalibration?.ToString("dd/MM/yyyy") ?? "-";
}

public record CreateEquipmentRequest(
    string? InternalId,
    string? AssetTag,
    string Name,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? SoftwareVersion,
    string? FirmwareVersion,
    string? Location,
    DateTime? InstalledAt,
    DateTime? ReceptionDate,
    string? ReceptionCondition,
    int? CalibrationFrequencyMonths,
    string? ManualPath
);

public record UpdateEquipmentRequest(
    Guid Id,
    string? InternalId,
    string? AssetTag,
    string Name,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? SoftwareVersion,
    string? FirmwareVersion,
    string? Location,
    DateTime? InstalledAt,
    DateTime? ReceptionDate,
    string? ReceptionCondition,
    DateTime? VerificationDate,
    bool IsVerified,
    int? CalibrationFrequencyMonths,
    DateTime? LastCalibration,
    DateTime? NextCalibration,
    string? ManualPath
);

public record RegisterMaintenanceRequest(
    Guid EquipmentId,
    Guid? PlanId,
    DateTime? PerformedAt,
    MaintenanceEventType EventType,
    string? Outcome,
    string? Notes,
    Guid? EvidenceDocId,
    bool? HasIssues,
    int? NextMaintenanceMonth,
    int? NextMaintenanceYear,
    Guid? UserId = null,
    string? CertificatePath = null,
    decimal? Cost = null,
    bool IsEfficiencyCheck = false
);

public record UpdateMaintenanceRequest(
    Guid Id,
    Guid EquipmentId,
    DateTime? PerformedAt,
    MaintenanceEventType EventType,
    string? Outcome,
    string? Notes,
    bool? HasIssues,
    int? NextMaintenanceMonth,
    int? NextMaintenanceYear,
    Guid? PerformedByUserId,
    string? CertificatePath = null,
    decimal? Cost = null,
    bool IsEfficiencyCheck = false
);

public record CreateDailyQCRequest(
    Guid EquipmentId,
    string LotNumber,
    bool IsPass,
    string? Notes,
    DateTime PerformedAt,
    Guid? UserId = null // Added for local tracking
);

public record EquipmentDailyQCDto(
    Guid Id,
    Guid EquipmentId,
    string LotNumber,
    bool IsPass,
    string? Notes,
    DateTime PerformedAt,
    string PerformedByName
);
