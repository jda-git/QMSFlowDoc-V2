using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Shared.Models;

public enum EquipmentStatus
{
    ACTIVE,
    PENDING_VERIFICATION, // New: Blocked until verified
    OUT_OF_SERVICE,       // New: Blocked due to failure
    RETIRED
}

public enum MaintenanceEventType
{
    PREVENTIVE,
    CORRECTIVE,
    INSPECTION,
    CALIBRATION,
    VERIFICATION, // New
    CLEANING      // New
}

public class Equipment
{
    public Guid Id { get; set; }
    public string? InternalId { get; set; } // Lab ID (e.g. EQ-001)
    public string? AssetTag { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? SoftwareVersion { get; set; } // ISO 15189 Req 3.1
    public string? FirmwareVersion { get; set; } // ISO 15189 Req 3.1
    public string? Location { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.PENDING_VERIFICATION; // Default secure state
    public DateTime? InstalledAt { get; set; }
    public string? Notes { get; set; }
    
    // Lifecycle & Metrology
    public DateTime? ReceptionDate { get; set; }
    public string? ReceptionCondition { get; set; } // New, Used, Damaged
    public DateTime? VerificationDate { get; set; }
    public bool IsVerified { get; set; } = false;
    
    public int? CalibrationFrequencyMonths { get; set; }
    public DateTime? LastCalibration { get; set; }
    public DateTime? NextCalibration { get; set; }
    public string? ManualPath { get; set; } // Path to PDF
    public bool IsDeleted { get; set; } = false;

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<MaintenancePlan> MaintenancePlans { get; set; } = new();
    public List<MaintenanceEvent> MaintenanceEvents { get; set; } = new();
    public List<EquipmentDailyQC> DailyQCs { get; set; } = new();
}

public class MaintenancePlan
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int FrequencyDays { get; set; }
    public string ChecklistJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
}

public class MaintenanceEvent
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public Guid? PlanId { get; set; }
    public DateTime PerformedAt { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public MaintenanceEventType EventType { get; set; }
    public string? Outcome { get; set; }
    public string? Notes { get; set; }
    public Guid? EvidenceDocId { get; set; }
    public bool? HasIssues { get; set; }
    public int? NextMaintenanceMonth { get; set; }
    public int? NextMaintenanceYear { get; set; }
    
    // ISO 15189 Extensions
    public string? CertificatePath { get; set; } // Calibration Cert
    public decimal? Cost { get; set; }
    public bool IsEfficiencyCheck { get; set; } // For Verification

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class EquipmentHistory
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public DateTime Date { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty; // STATUS_CHANGE, LOCATION_CHANGE, MAINTENANCE, CALIBRATION
    public string Description { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
