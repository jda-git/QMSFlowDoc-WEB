using System;
using System.Collections.Generic;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public class EquipmentListDto
{
    public Guid Id { get; set; }
    public string? InternalId { get; set; }
    public string? AssetTag { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Model { get; set; }
    public string? Manufacturer { get; set; }
    public string? SerialNumber { get; set; }
    public string? Location { get; set; }
    public string? AreaLaboratorio { get; set; }
    public EquipmentStatus Status { get; set; }
    public EquipmentCriticidad Criticidad { get; set; }
    public EquipmentAptitude Aptitude { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? NextCalibration { get; set; }
    public string? Restrictions { get; set; }
    
    // Summary data
    public Guid? LastMaintenanceEventId { get; set; }
    public DateTime? LastMaintenanceAt { get; set; }
    public string? LastEventType { get; set; }
    public string? LastOutcome { get; set; }
    public string? NextMaintenanceDue { get; set; }
    public string? TodayQCStatus { get; set; } // "PASS", "FAIL", "PENDING"
    public string? TodayQCColor { get; set; }
    
    public int ActiveAlertCount { get; set; }
    public string? Observations { get; set; }

    public EquipmentListDto() { }
    
    public string LastMaintenanceDateFormatted => LastMaintenanceAt?.ToString("dd/MM/yyyy") ?? "-";
    public string StatusFormatted => Status switch
    {
        EquipmentStatus.IN_SERVICE => "En servicio",
        EquipmentStatus.PENDING_QC_VERIFICATION => "Pendiente de QC/Verificación",
        EquipmentStatus.OUT_OF_SERVICE => "Fuera de servicio",
        EquipmentStatus.RETIRED => "Retirado",
        EquipmentStatus.PENDING_RECEIPT => "Pendiente de recepción",
        EquipmentStatus.RECEIVED => "Recibido",
        EquipmentStatus.PENDING_ACCEPTANCE => "Pendiente de aceptación",
        EquipmentStatus.ACCEPTED => "Aceptado",
        EquipmentStatus.IN_SERVICE_WITH_RESTRICTIONS => "En servicio con restricciones",
        EquipmentStatus.QC_NON_CONFORMING => "QC no conforme",
        EquipmentStatus.IN_MAINTENANCE => "En mantenimiento",
        EquipmentStatus.IN_REPAIR => "En reparación",
        EquipmentStatus.IN_QUARANTINE => "En cuarentena",
        EquipmentStatus.OBSOLETE => "Obsoleto",
        EquipmentStatus.DECOMMISSIONED => "Baja definitiva",
        _ => Status.ToString()
    };
    public string NextCalibrationFormatted => NextCalibration?.ToString("dd/MM/yyyy") ?? "-";
    public string CriticidadFormatted => Criticidad switch
    {
        EquipmentCriticidad.CRITICAL => "Crítico",
        EquipmentCriticidad.SEMICRITICAL => "Semicrítico",
        EquipmentCriticidad.AUXILIARY => "Auxiliar",
        EquipmentCriticidad.NON_CRITICAL => "No crítico",
        _ => Criticidad.ToString()
    };
}

public class EquipmentDetailDto
{
    public Equipment Equipment { get; set; } = new();
    public EquipmentAcceptance? Acceptance { get; set; }
    public List<MaintenancePlan> MaintenancePlans { get; set; } = new();
    public List<MaintenanceEvent> MaintenanceEvents { get; set; } = new();
    public List<EquipmentFunctionalQC> FunctionalQCs { get; set; } = new();
    public List<EquipmentCalibrationPlan> CalibrationPlans { get; set; } = new();
    public List<EquipmentCalibrationRecord> CalibrationRecords { get; set; } = new();
    public List<EquipmentRepairRecord> RepairRecords { get; set; } = new();
    public List<EquipmentIncident> Incidents { get; set; } = new();
    public List<EquipmentImpactAssessment> ImpactAssessments { get; set; } = new();
    public List<EquipmentDecommission> Decommissions { get; set; } = new();
    public List<EquipmentStatusHistory> StatusHistory { get; set; } = new();
    public List<EquipmentAlert> ActiveAlerts { get; set; } = new();
    public List<EquipmentHistory> AuditHistory { get; set; } = new();
}

public class CreateEquipmentRequest
{
    public string? InternalId { get; set; }
    public string? AssetTag { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? SoftwareVersion { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? Location { get; set; }
    
    public EquipmentCriticidad Criticidad { get; set; } = EquipmentCriticidad.SEMICRITICAL;
    public string? HospitalInventoryNumber { get; set; }
    public string? AreaLaboratorio { get; set; }
    public string? Description { get; set; }
    public string? IntendedUse { get; set; }
    
    public DateTime? PurchaseDate { get; set; }
    public DateTime? InstalledAt { get; set; }
    public DateTime? WarrantyUntil { get; set; }
    public bool HasMaintenanceContract { get; set; }
    public string? TechnicalServiceInfo { get; set; }
    public string? Notes { get; set; }
    
    // Potential Impact Flags
    public bool ImpactResult { get; set; }
    public bool ImpactPreservation { get; set; }
    public bool ImpactTraceability { get; set; }
    public bool ImpactBiosecurity { get; set; }
    public bool ImpactContinuity { get; set; }
    public bool ImpactPreparation { get; set; }
    public bool ImpactAnalysis { get; set; }

    public DateTime? ReceptionDate { get; set; }
    public string? ReceptionCondition { get; set; }
    public int? CalibrationFrequencyMonths { get; set; }
    public string? ManualPath { get; set; }

    public CreateEquipmentRequest() { }

    public CreateEquipmentRequest(
        string? internalId,
        string? assetTag,
        string name,
        string? manufacturer,
        string? model,
        string? serialNumber,
        string? softwareVersion,
        string? firmwareVersion,
        string? location,
        DateTime? installedAt,
        DateTime? receptionDate,
        string? receptionCondition,
        int calibrationFrequencyMonths,
        string? manualPath)
    {
        InternalId = internalId;
        AssetTag = assetTag;
        Name = name;
        Manufacturer = manufacturer;
        Model = model;
        SerialNumber = serialNumber;
        SoftwareVersion = softwareVersion;
        FirmwareVersion = firmwareVersion;
        Location = location;
        InstalledAt = installedAt;
        ReceptionDate = receptionDate;
        ReceptionCondition = receptionCondition;
        CalibrationFrequencyMonths = calibrationFrequencyMonths;
        ManualPath = manualPath;
    }

    // Technical specifications
    public string? CytoType { get; set; }
    public int? CytoLasersCount { get; set; }
    public string? CytoWavelengths { get; set; }
    public int? CytoDetectorsCount { get; set; }
    public string? CytoOpticalConfig { get; set; }
    public string? CytoFilters { get; set; }
    public string? CytoParameters { get; set; }
    public string? CytoAcquisitionSoftware { get; set; }
    public string? CytoAcquisitionSoftwareVersion { get; set; }
    public string? CytoAssociatedComputer { get; set; }
    public string? CytoOS { get; set; }
    public string? CytoAcquisitionConfig { get; set; }
    public string? CytoQcConfig { get; set; }
    public bool CytoFcsExport { get; set; }
    public string? CytoFcsExportPath { get; set; }

    public string? PipetteType { get; set; }
    public decimal? PipetteNominalVolume { get; set; }
    public string? PipetteVolumeRange { get; set; }
    public decimal? PipetteResolution { get; set; }
    public bool PipetteCriticalUse { get; set; }
    public string? PipetteIntendedUse { get; set; }
    public string? PipetteEmpLimit { get; set; }

    public decimal? ColdTempMin { get; set; }
    public decimal? ColdTempMax { get; set; }
    public string? ColdTempRecordType { get; set; }
    public string? ColdSensorAssociated { get; set; }
    public bool ColdHasAlarm { get; set; }
    public bool ColdHasBackup { get; set; }

    public string? CentrifugeType { get; set; }
    public string? CentrifugeRotor { get; set; }
    public string? CentrifugeRpmRange { get; set; }
    public string? CentrifugeRcfRange { get; set; }
    public bool CentrifugeHasTimer { get; set; }
    public string? CentrifugeSpecificMaintenance { get; set; }

    public string? SoftName { get; set; }
    public string? SoftManufacturer { get; set; }
    public string? SoftVersion { get; set; }
    public string? SoftLicense { get; set; }
    public string? SoftComputerInstalled { get; set; }
    public string? SoftFunction { get; set; }
    public DateTime? SoftInstallationDate { get; set; }
    public string? SoftValidationState { get; set; }
}

public class UpdateEquipmentRequest : CreateEquipmentRequest
{
    public Guid Id { get; set; }
    public EquipmentStatus Status { get; set; }
    public EquipmentAptitude Aptitude { get; set; }
    public string? Restrictions { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerificationDate { get; set; }
    public DateTime? LastCalibration { get; set; }
    public DateTime? NextCalibration { get; set; }

    public UpdateEquipmentRequest() { }

    public UpdateEquipmentRequest(
        Guid id,
        string? internalId,
        string? assetTag,
        string name,
        string? manufacturer,
        string? model,
        string? serialNumber,
        string? softwareVersion,
        string? firmwareVersion,
        string? location,
        DateTime? installedAt,
        DateTime? receptionDate,
        string? receptionCondition,
        DateTime? verificationDate,
        bool isVerified,
        int calibrationFrequencyMonths,
        DateTime? lastCalibration,
        DateTime? nextCalibration,
        string? manualPath)
    {
        Id = id;
        InternalId = internalId;
        AssetTag = assetTag;
        Name = name;
        Manufacturer = manufacturer;
        Model = model;
        SerialNumber = serialNumber;
        SoftwareVersion = softwareVersion;
        FirmwareVersion = firmwareVersion;
        Location = location;
        InstalledAt = installedAt;
        ReceptionDate = receptionDate;
        ReceptionCondition = receptionCondition;
        VerificationDate = verificationDate;
        IsVerified = isVerified;
        CalibrationFrequencyMonths = calibrationFrequencyMonths;
        LastCalibration = lastCalibration;
        NextCalibration = nextCalibration;
        ManualPath = manualPath;
    }
}

public class RegisterAcceptanceRequest
{
    public Guid EquipmentId { get; set; }
    public DateTime? ReceptionDate { get; set; }
    public string? ReceptionCondition { get; set; }
    public bool PackagingCorrect { get; set; }
    public bool VisualDamage { get; set; }
    public bool AccessoriesReceived { get; set; }
    public string? ReceptionNotes { get; set; }
    public string? ReceptionEvidencePath { get; set; }
    
    public DateTime? InstallationDate { get; set; }
    public string? InstalledBy { get; set; }
    public bool AmbientConditionsCorrect { get; set; }
    public bool ConnectionsCorrect { get; set; }
    public bool InitialPowerOnCorrect { get; set; }
    public bool SoftwareCommunicationCorrect { get; set; }
    public string? InstallationNotes { get; set; }
    public string? InstallationEvidencePath { get; set; }
    
    public DateTime? AcceptanceDate { get; set; }
    public bool CriteriaDefined { get; set; }
    public string? CriteriaMet { get; set; }
    public string? AcceptanceOutcome { get; set; }
    public string? AcceptanceRestrictions { get; set; }
    public DateTime? ServiceEntryDate { get; set; }
    public string? AcceptanceEvidencePath { get; set; }
    public string? AcceptanceNotes { get; set; }
}

public class CreateDailyQCRequest
{
    public Guid EquipmentId { get; set; }
    public QCType Type { get; set; } = QCType.DIARIO;
    public string LotNumber { get; set; } = string.Empty;
    public string? ParametersEvaluated { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public string? ObtainedValues { get; set; }
    public QCOutcome Outcome { get; set; } = QCOutcome.CONFORME;
    public bool IsPass { get; set; } = true;
    public string? EvidencePath { get; set; }
    public string? Notes { get; set; }
    public string? ActionTaken { get; set; }
    public EquipmentStatus EquipmentEndStatus { get; set; } = EquipmentStatus.IN_SERVICE;
    public Guid? UserId { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    public CreateDailyQCRequest() { }

    public CreateDailyQCRequest(
        Guid equipmentId,
        string lotNumber,
        bool isPass,
        string? notes,
        DateTime performedAt,
        Guid? userId)
    {
        EquipmentId = equipmentId;
        LotNumber = lotNumber;
        IsPass = isPass;
        Notes = notes;
        PerformedAt = performedAt;
        UserId = userId;
    }
}

public class RegisterMaintenanceRequest
{
    public Guid EquipmentId { get; set; }
    public Guid? PlanId { get; set; }
    public string? PlanName { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? PerformedAt { get; set; }
    public MaintenanceEventType EventType { get; set; } = MaintenanceEventType.PREVENTIVE;
    public bool IsInternal { get; set; } = true;
    public string? ActivitiesPerformed { get; set; }
    public string? Outcome { get; set; }
    public bool HasDeviation { get; set; }
    public string? DeviationReason { get; set; }
    public bool RequiresAdditionalAction { get; set; }
    public bool RequiresVerification { get; set; }
    public bool VerificationPerformed { get; set; }
    public EquipmentStatus? EndStatus { get; set; }
    public string? Notes { get; set; }
    public Guid? EvidenceDocId { get; set; }
    public string? CertificatePath { get; set; }
    public decimal? Cost { get; set; }
    public bool IsEfficiencyCheck { get; set; }
    public int? NextMaintenanceMonth { get; set; }
    public int? NextMaintenanceYear { get; set; }
    public Guid? UserId { get; set; }
    public bool? HasIssues { get; set; }

    public RegisterMaintenanceRequest() { }

    public RegisterMaintenanceRequest(
        Guid equipmentId,
        Guid? planId,
        DateTime? performedAt,
        MaintenanceEventType eventType,
        string? outcome,
        string? notes,
        Guid? evidenceDocId,
        bool? hasIssues,
        int? nextMaintenanceMonth,
        int? nextMaintenanceYear,
        Guid? userId,
        string? certificatePath)
    {
        EquipmentId = equipmentId;
        PlanId = planId;
        PerformedAt = performedAt;
        EventType = eventType;
        Outcome = outcome;
        Notes = notes;
        EvidenceDocId = evidenceDocId;
        HasIssues = hasIssues;
        NextMaintenanceMonth = nextMaintenanceMonth;
        NextMaintenanceYear = nextMaintenanceYear;
        UserId = userId;
        CertificatePath = certificatePath;
    }
}

public class RegisterCalibrationPlanRequest
{
    public Guid EquipmentId { get; set; }
    public string ControlledMagnitude { get; set; } = string.Empty;
    public int FrequencyMonths { get; set; }
    public string? Tolerance { get; set; }
    public string? ProviderOrMethod { get; set; }
    public bool RequiresCertificate { get; set; }
    public string? Notes { get; set; }
}

public class RegisterMaintenancePlanRequest
{
    public Guid EquipmentId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int FrequencyDays { get; set; }
    public string ChecklistJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    
    public DateTime? StartDate { get; set; }
    public DateTime? NextDueDate { get; set; }
    public int? ToleranceDays { get; set; }
    public string? Responsible { get; set; }
    public bool RequiresStop { get; set; }
    public bool RequiresVerification { get; set; }
}

public class RegisterCalibrationRecordRequest
{
    public Guid EquipmentId { get; set; }
    public Guid? PlanId { get; set; }
    public DateTime PerformedAt { get; set; }
    public string Type { get; set; } = "Calibración";
    public string Magnitude { get; set; } = string.Empty;
    public CalibrationOutcome Outcome { get; set; } = CalibrationOutcome.APTO;
    public string? ObservedError { get; set; }
    public string? MaxPermissibleError { get; set; }
    public string? Uncertainty { get; set; }
    public string? CertificatePath { get; set; }
    public DateTime? NextDueDate { get; set; }
    public string? Restrictions { get; set; }
    public bool ImpactAssessmentRequired { get; set; }
    public string? Notes { get; set; }

    // Pipette specific
    public decimal? VolumeNominal { get; set; }
    public decimal? VolumeTested { get; set; }
    public decimal? SystematicError { get; set; }
    public decimal? RandomError { get; set; }
    public decimal? AcceptableLimit { get; set; }
    public string? PointsResultsJson { get; set; }
    public Guid? UserId { get; set; }
}

public class RegisterRepairRequest
{
    public Guid EquipmentId { get; set; }
    public DateTime DetectionDate { get; set; }
    public string ProblemDescription { get; set; } = string.Empty;
    public string DetectedDuring { get; set; } = string.Empty;
    public string Severity { get; set; } = "Baja";
    public bool EquipmentRemovedFromService { get; set; }
    public DateTime? RemovedFromServiceDate { get; set; }
    public bool TechnicalServiceNotified { get; set; }
    public DateTime? NotificationDate { get; set; }
    
    public DateTime? InterventionDate { get; set; }
    public string? InterventionDescription { get; set; }
    public string? PartsReplaced { get; set; }
    public bool ConfigurationModified { get; set; }
    public bool SoftwareUpdated { get; set; }
    public string Outcome { get; set; } = "Resuelto";
    public bool VerificationRequired { get; set; } = true;
    public bool VerificationPerformed { get; set; }
    public DateTime? ReactivationDate { get; set; }
    public EquipmentStatus EndStatus { get; set; } = EquipmentStatus.IN_SERVICE;
    public string? EvidencePath { get; set; }
    public string? Notes { get; set; }
    public string? PerformedBy { get; set; }
    public Guid? UserId { get; set; }
}

public class RegisterIncidentRequest
{
    public Guid EquipmentId { get; set; }
    public DateTime IncidentDate { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Baja";
    public EquipmentStatus ImmediateStatus { get; set; } = EquipmentStatus.OUT_OF_SERVICE;
    public string ImmediateAction { get; set; } = string.Empty;
    public bool RequiresRemoval { get; set; }
    public bool RequiresRepair { get; set; }
    public bool RequiresImpactAssessment { get; set; }
    public bool RequiresNotification { get; set; }
    public string? EvidencePath { get; set; }
    public Guid? UserId { get; set; }
}

public class RegisterImpactRequest
{
    public Guid EquipmentId { get; set; }
    public Guid? IncidentId { get; set; }
    public Guid? RepairId { get; set; }
    public DateTime? LastConformingVerificationDate { get; set; }
    public DateTime? ProbableStartDateOfProblem { get; set; }
    public string? PotentiallyAffectedPeriod { get; set; }
    public string ImpactType { get; set; } = "Sin impacto esperado";
    public bool RequiresExternalReview { get; set; }
    public Guid? ExternalNCId { get; set; }
    public string Decision { get; set; } = "Sin acciones adicionales";
    public string Justification { get; set; } = string.Empty;
    public EquipmentStatus EndStatus { get; set; } = EquipmentStatus.IN_SERVICE;
    public string? EvidencePath { get; set; }
    public Guid? UserId { get; set; }
}

public class RegisterDecommissionRequest
{
    public Guid EquipmentId { get; set; }
    public string Type { get; set; } = "Cuarentena";
    public string Reason { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public bool RequiresDecontamination { get; set; }
    public bool DecontaminationPerformed { get; set; }
    public string? DecontaminationEvidencePath { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid UserId { get; set; }
}

public class EquipmentDashboardDto
{
    public int TotalEquipments { get; set; }
    public int CriticalEquipments { get; set; }
    public double ActivePercentage { get; set; }
    public double OutOfServicePercentage { get; set; }
    public double MaintenanceInTimePercentage { get; set; }
    public double CalibrationInTimePercentage { get; set; }
    public int ActiveIncidentsCount { get; set; }
    public int PendingAcceptanceCount { get; set; }
    public int PendingVerificationPostRepairCount { get; set; }
    public double CytometersAvailability { get; set; } // Availability of critical conventional/spectral analyzers
    public int CytometersDowntimeDays { get; set; }
    public List<EquipmentAlert> ActiveAlerts { get; set; } = new();
}

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

public record EquipmentDailyQCDto(
    Guid Id,
    Guid EquipmentId,
    string LotNumber,
    bool IsPass,
    string? Notes,
    DateTime PerformedAt,
    string PerformedByName
);
