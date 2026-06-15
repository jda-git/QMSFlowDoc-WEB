using System;

namespace QMSFlowDoc.Domain.Entities;

public enum QCType
{
    DIARIO,
    SEMANAL,
    MENSUAL,
    TRAS_MANTENIMIENTO,
    TRAS_REPARACION,
    TRAS_CAMBIO_CONFIGURACION,
    EXTRAORDINARIO
}

public enum QCOutcome
{
    CONFORME,
    NO_CONFORME,
    CONFORME_CON_OBSERVACIONES
}

public class EquipmentFunctionalQC
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    
    // Traceability
    public DateTime PerformedAt { get; set; }
    public Guid PerformedByUserId { get; set; }
    public string PerformedByUserName { get; set; } = string.Empty;
    
    // QC Details
    public QCType Type { get; set; } = QCType.DIARIO;
    public string? LotNumber { get; set; } // Beads, control materials lot (for cytometers, etc.)
    public string? ParametersEvaluated { get; set; } // e.g. Laser delay, CV, PE, FITC channel intensities
    public string? AcceptanceCriteria { get; set; } // e.g. CV < 3%
    public string? ObtainedValues { get; set; } // e.g. CV = 2.1%
    public QCOutcome Outcome { get; set; } = QCOutcome.CONFORME;
    public bool IsPass { get; set; } = true; // Overall pass/fail
    
    public string? EvidencePath { get; set; } // PDF or screenshot path
    public string? Notes { get; set; }
    public string? ActionTaken { get; set; } // Actions if QC failed
    public EquipmentStatus EquipmentEndStatus { get; set; } = EquipmentStatus.IN_SERVICE;

    // Soft delete / anulación
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public string? VoidReason { get; set; }

    // Navigation property
    public Equipment? Equipment { get; set; }
}
