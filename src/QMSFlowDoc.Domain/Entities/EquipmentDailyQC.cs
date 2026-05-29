using QMSFlowDoc.Domain.Identity;
using System;

namespace QMSFlowDoc.Domain.Entities;

public class EquipmentDailyQC
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    
    // ISO 15189: Traceability of reagents/controls
    public string LotNumber { get; set; } = string.Empty; 
    
    // Result of the QC
    public bool IsPass { get; set; }
    
    // Optional measurements or notes
    public string? Notes { get; set; }
    
    // Who performed it
    public Guid PerformedByUserId { get; set; }
    public DateTime PerformedAt { get; set; }
    
    // Navigation properties
    public Equipment? Equipment { get; set; }
    // ApplicationUser navigation property might be needed if we want to show "Performed By Name" easily
}
