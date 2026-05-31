using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Application.Services.Equipment;
using QMSFlowDoc.Infrastructure.Persistence;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using DomainEntities = QMSFlowDoc.Domain.Entities;
using SharedModels = QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Infrastructure.Services.Equipment;

public class EquipmentService : IEquipmentService
{
    private readonly QmsDbContext _context;

    public EquipmentService(QmsDbContext context)
    {
        _context = context;
    }

    // ── Audit Helper ──
    private async Task LogHistoryAsync(Guid equipmentId, Guid userId, string actionType, string description, string? oldValue = null, string? newValue = null, string? notes = null, string? evidencePath = null)
    {
        var user = await _context.Users.FindAsync(userId);
        var userName = user?.FullName ?? "Sistema";

        var history = new DomainEntities.EquipmentHistory
        {
            Id = Guid.NewGuid(),
            EquipmentId = equipmentId,
            Date = DateTime.UtcNow,
            UserId = userId,
            UserName = userName,
            ActionType = actionType,
            Description = description,
            OldValue = oldValue,
            NewValue = newValue,
            Notes = notes,
            EvidencePath = evidencePath
        };

        _context.EquipmentHistory.Add(history);
    }

    private async Task LogAuditAsync(string action, string entityType, Guid? entityId, string details, Guid? userId, string? username)
    {
        var audit = new DomainEntities.AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            UserId = userId ?? Guid.Empty,
            UserName = username ?? "Sistema",
            Timestamp = DateTime.UtcNow,
            MachineName = Environment.MachineName,
            Result = "Success"
        };
        _context.AuditLogs.Add(audit);
    }

    // ── Inventory CRUD ──
    public async Task<List<EquipmentListDto>> GetEquipmentsAsync()
    {
        var list = await _context.Equipments
            .Include(e => e.MaintenanceEvents)
            .Include(e => e.FunctionalQCs)
            .ToListAsync();

        var alerts = await _context.EquipmentAlerts.Where(a => a.IsActive).ToListAsync();

        var dtos = new List<EquipmentListDto>();

        foreach (var e in list)
        {
            var activeAlerts = alerts.Where(a => a.EquipmentId == e.Id).ToList();
            var lastMaint = e.MaintenanceEvents.OrderByDescending(ev => ev.PerformedAt).FirstOrDefault();
            var lastQC = e.FunctionalQCs.OrderByDescending(qc => qc.PerformedAt).FirstOrDefault();

            string qcStatus = "PENDING";
            string qcColor = "text-secondary";
            if (lastQC != null)
            {
                if (lastQC.PerformedAt.Date == DateTime.UtcNow.Date)
                {
                    qcStatus = lastQC.IsPass ? "PASS" : "FAIL";
                    qcColor = lastQC.IsPass ? "text-success" : "text-danger";
                }
            }

            dtos.Add(new EquipmentListDto
            {
                Id = e.Id,
                InternalId = e.InternalId,
                AssetTag = e.AssetTag,
                Name = e.Name,
                Model = e.Model,
                Manufacturer = e.Manufacturer,
                SerialNumber = e.SerialNumber,
                Location = e.Location,
                AreaLaboratorio = e.AreaLaboratorio,
                Status = (SharedModels.EquipmentStatus)e.Status,
                Criticidad = (SharedModels.EquipmentCriticidad)e.Criticidad,
                Aptitude = (SharedModels.EquipmentAptitude)e.Aptitude,
                IsVerified = e.IsVerified,
                NextCalibration = e.NextCalibration,
                Restrictions = e.Restrictions,
                ActiveAlertCount = activeAlerts.Count,
                Observations = e.Notes,
                LastMaintenanceEventId = lastMaint?.Id,
                LastMaintenanceAt = lastMaint?.PerformedAt,
                LastEventType = lastMaint?.EventType.ToString(),
                LastOutcome = lastMaint?.Outcome,
                TodayQCStatus = qcStatus,
                TodayQCColor = qcColor
            });
        }

        return dtos;
    }

    public async Task<EquipmentDetailDto?> GetEquipmentDetailsAsync(Guid id)
    {
        var e = await _context.Equipments
            .Include(eq => eq.MaintenancePlans)
            .Include(eq => eq.MaintenanceEvents)
            .FirstOrDefaultAsync(eq => eq.Id == id);

        if (e == null) return null;

        var acceptance = await _context.EquipmentAcceptances.FirstOrDefaultAsync(a => a.EquipmentId == id);
        var qcs = await _context.EquipmentFunctionalQC.Where(q => q.EquipmentId == id).OrderByDescending(q => q.PerformedAt).ToListAsync();
        var calPlans = await _context.EquipmentCalibrationPlans.Where(p => p.EquipmentId == id).ToListAsync();
        var calRecords = await _context.EquipmentCalibrationRecords.Where(r => r.EquipmentId == id).OrderByDescending(r => r.PerformedAt).ToListAsync();
        var repairs = await _context.EquipmentRepairRecords.Where(r => r.EquipmentId == id).OrderByDescending(r => r.DetectionDate).ToListAsync();
        var incidents = await _context.EquipmentIncidents.Where(i => i.EquipmentId == id).OrderByDescending(i => i.IncidentDate).ToListAsync();
        var impacts = await _context.EquipmentImpactAssessments.Where(a => a.EquipmentId == id).OrderByDescending(a => a.ClosedAt).ToListAsync();
        var decommissions = await _context.EquipmentDecommissions.Where(d => d.EquipmentId == id).OrderByDescending(d => d.Date).ToListAsync();
        var history = await _context.EquipmentHistory.Where(h => h.EquipmentId == id).OrderByDescending(h => h.Date).ToListAsync();
        var alerts = await _context.EquipmentAlerts.Where(a => a.EquipmentId == id && a.IsActive).ToListAsync();

        // Map domain objects to shared contract objects
        var sharedEquipment = new SharedModels.Equipment
        {
            Id = e.Id,
            InternalId = e.InternalId,
            AssetTag = e.AssetTag,
            Name = e.Name,
            Manufacturer = e.Manufacturer,
            Model = e.Model,
            SerialNumber = e.SerialNumber,
            SoftwareVersion = e.SoftwareVersion,
            FirmwareVersion = e.FirmwareVersion,
            Location = e.Location,
            Status = (SharedModels.EquipmentStatus)e.Status,
            Criticidad = (SharedModels.EquipmentCriticidad)e.Criticidad,
            Aptitude = (SharedModels.EquipmentAptitude)e.Aptitude,
            Restrictions = e.Restrictions,
            HospitalInventoryNumber = e.HospitalInventoryNumber,
            AreaLaboratorio = e.AreaLaboratorio,
            Description = e.Description,
            IntendedUse = e.IntendedUse,
            PurchaseDate = e.PurchaseDate,
            InstalledAt = e.InstalledAt,
            ReceptionDate = e.ReceptionDate,
            ReceptionCondition = e.ReceptionCondition,
            AcceptanceDate = e.AcceptanceDate,
            ServiceEntryDate = e.ServiceEntryDate,
            DecommissionDate = e.DecommissionDate,
            WarrantyUntil = e.WarrantyUntil,
            HasMaintenanceContract = e.HasMaintenanceContract,
            TechnicalServiceInfo = e.TechnicalServiceInfo,
            Notes = e.Notes,
            IsDeleted = e.IsDeleted,
            VerificationDate = e.VerificationDate,
            IsVerified = e.IsVerified,
            CalibrationFrequencyMonths = e.CalibrationFrequencyMonths,
            LastCalibration = e.LastCalibration,
            NextCalibration = e.NextCalibration,
            ManualPath = e.ManualPath,
            ImpactResult = e.ImpactResult,
            ImpactPreservation = e.ImpactPreservation,
            ImpactTraceability = e.ImpactTraceability,
            ImpactBiosecurity = e.ImpactBiosecurity,
            ImpactContinuity = e.ImpactContinuity,
            ImpactPreparation = e.ImpactPreparation,
            ImpactAnalysis = e.ImpactAnalysis,

            // Specs
            CytoType = e.CytoType,
            CytoLasersCount = e.CytoLasersCount,
            CytoWavelengths = e.CytoWavelengths,
            CytoDetectorsCount = e.CytoDetectorsCount,
            CytoOpticalConfig = e.CytoOpticalConfig,
            CytoFilters = e.CytoFilters,
            CytoParameters = e.CytoParameters,
            CytoAcquisitionSoftware = e.CytoAcquisitionSoftware,
            CytoAcquisitionSoftwareVersion = e.CytoAcquisitionSoftwareVersion,
            CytoAssociatedComputer = e.CytoAssociatedComputer,
            CytoOS = e.CytoOS,
            CytoAcquisitionConfig = e.CytoAcquisitionConfig,
            CytoQcConfig = e.CytoQcConfig,
            CytoFcsExport = e.CytoFcsExport,
            CytoFcsExportPath = e.CytoFcsExportPath,

            PipetteType = e.PipetteType,
            PipetteNominalVolume = e.PipetteNominalVolume,
            PipetteVolumeRange = e.PipetteVolumeRange,
            PipetteResolution = e.PipetteResolution,
            PipetteCriticalUse = e.PipetteCriticalUse,
            PipetteIntendedUse = e.PipetteIntendedUse,
            PipetteEmpLimit = e.PipetteEmpLimit,

            ColdTempMin = e.ColdTempMin,
            ColdTempMax = e.ColdTempMax,
            ColdTempRecordType = e.ColdTempRecordType,
            ColdSensorAssociated = e.ColdSensorAssociated,
            ColdHasAlarm = e.ColdHasAlarm,
            ColdHasBackup = e.ColdHasBackup,

            CentrifugeType = e.CentrifugeType,
            CentrifugeRotor = e.CentrifugeRotor,
            CentrifugeRpmRange = e.CentrifugeRpmRange,
            CentrifugeRcfRange = e.CentrifugeRcfRange,
            CentrifugeHasTimer = e.CentrifugeHasTimer,
            CentrifugeSpecificMaintenance = e.CentrifugeSpecificMaintenance,

            SoftName = e.SoftName,
            SoftManufacturer = e.SoftManufacturer,
            SoftVersion = e.SoftVersion,
            SoftLicense = e.SoftLicense,
            SoftComputerInstalled = e.SoftComputerInstalled,
            SoftFunction = e.SoftFunction,
            SoftInstallationDate = e.SoftInstallationDate,
            SoftValidationState = e.SoftValidationState
        };

        var sharedAcceptance = acceptance == null ? null : new SharedModels.EquipmentAcceptance
        {
            Id = acceptance.Id,
            EquipmentId = acceptance.EquipmentId,
            ReceptionDate = acceptance.ReceptionDate,
            ReceptionCondition = acceptance.ReceptionCondition,
            PackagingCorrect = acceptance.PackagingCorrect,
            VisualDamage = acceptance.VisualDamage,
            AccessoriesReceived = acceptance.AccessoriesReceived,
            ReceptionNotes = acceptance.ReceptionNotes,
            ReceptionEvidencePath = acceptance.ReceptionEvidencePath,
            InstallationDate = acceptance.InstallationDate,
            InstalledBy = acceptance.InstalledBy,
            AmbientConditionsCorrect = acceptance.AmbientConditionsCorrect,
            ConnectionsCorrect = acceptance.ConnectionsCorrect,
            InitialPowerOnCorrect = acceptance.InitialPowerOnCorrect,
            SoftwareCommunicationCorrect = acceptance.SoftwareCommunicationCorrect,
            InstallationNotes = acceptance.InstallationNotes,
            InstallationEvidencePath = acceptance.InstallationEvidencePath,
            AcceptanceDate = acceptance.AcceptanceDate,
            CriteriaDefined = acceptance.CriteriaDefined,
            CriteriaMet = acceptance.CriteriaMet,
            AcceptanceOutcome = acceptance.AcceptanceOutcome,
            AcceptanceRestrictions = acceptance.AcceptanceRestrictions,
            ServiceEntryDate = acceptance.ServiceEntryDate,
            AcceptanceEvidencePath = acceptance.AcceptanceEvidencePath,
            AcceptanceNotes = acceptance.AcceptanceNotes
        };

        var sharedMaintPlans = e.MaintenancePlans.Select(p => new SharedModels.MaintenancePlan
        {
            Id = p.Id,
            EquipmentId = p.EquipmentId,
            PlanName = p.PlanName,
            FrequencyDays = p.FrequencyDays,
            ChecklistJson = p.ChecklistJson,
            IsActive = p.IsActive,
            StartDate = p.StartDate,
            NextDueDate = p.NextDueDate,
            ToleranceDays = p.ToleranceDays,
            Responsible = p.Responsible,
            RequiresStop = p.RequiresStop,
            RequiresVerification = p.RequiresVerification
        }).ToList();

        var sharedMaintEvents = e.MaintenanceEvents.Select(ev => new SharedModels.MaintenanceEvent
        {
            Id = ev.Id,
            EquipmentId = ev.EquipmentId,
            PlanId = ev.PlanId,
            PlanName = ev.PlanName,
            ScheduledDate = ev.ScheduledDate,
            PerformedAt = ev.PerformedAt,
            PerformedByUserId = ev.PerformedByUserId,
            PerformedByUserName = ev.PerformedByUserName,
            EventType = (SharedModels.MaintenanceEventType)ev.EventType,
            IsInternal = ev.IsInternal,
            ActivitiesPerformed = ev.ActivitiesPerformed,
            Outcome = ev.Outcome,
            HasDeviation = ev.HasDeviation,
            DeviationReason = ev.DeviationReason,
            RequiresAdditionalAction = ev.RequiresAdditionalAction,
            RequiresVerification = ev.RequiresVerification,
            VerificationPerformed = ev.VerificationPerformed,
            EndStatus = ev.EndStatus.HasValue ? (SharedModels.EquipmentStatus)ev.EndStatus.Value : null,
            Notes = ev.Notes,
            EvidenceDocId = ev.EvidenceDocId,
            CertificatePath = ev.CertificatePath,
            Cost = ev.Cost,
            IsEfficiencyCheck = ev.IsEfficiencyCheck,
            HasIssues = ev.HasIssues,
            NextMaintenanceMonth = ev.NextMaintenanceMonth,
            NextMaintenanceYear = ev.NextMaintenanceYear
        }).OrderByDescending(ev => ev.PerformedAt).ToList();

        var sharedQcs = qcs.Select(qc => new SharedModels.EquipmentFunctionalQC
        {
            Id = qc.Id,
            EquipmentId = qc.EquipmentId,
            PerformedAt = qc.PerformedAt,
            PerformedByUserId = qc.PerformedByUserId,
            PerformedByUserName = qc.PerformedByUserName,
            Type = (SharedModels.QCType)qc.Type,
            LotNumber = qc.LotNumber,
            ParametersEvaluated = qc.ParametersEvaluated,
            AcceptanceCriteria = qc.AcceptanceCriteria,
            ObtainedValues = qc.ObtainedValues,
            Outcome = (SharedModels.QCOutcome)qc.Outcome,
            IsPass = qc.IsPass,
            EvidencePath = qc.EvidencePath,
            Notes = qc.Notes,
            ActionTaken = qc.ActionTaken,
            EquipmentEndStatus = (SharedModels.EquipmentStatus)qc.EquipmentEndStatus
        }).ToList();

        var sharedCalPlans = calPlans.Select(p => new SharedModels.EquipmentCalibrationPlan
        {
            Id = p.Id,
            EquipmentId = p.EquipmentId,
            ControlledMagnitude = p.ControlledMagnitude,
            FrequencyMonths = p.FrequencyMonths,
            LastCalibrationDate = p.LastCalibrationDate,
            NextCalibrationDate = p.NextCalibrationDate,
            Tolerance = p.Tolerance,
            ProviderOrMethod = p.ProviderOrMethod,
            RequiresCertificate = p.RequiresCertificate,
            IsActive = p.IsActive,
            Notes = p.Notes
        }).ToList();

        var sharedCalRecords = calRecords.Select(r => new SharedModels.EquipmentCalibrationRecord
        {
            Id = r.Id,
            EquipmentId = r.EquipmentId,
            PlanId = r.PlanId,
            PerformedAt = r.PerformedAt,
            PerformedByUserId = r.PerformedByUserId,
            PerformedByUserName = r.PerformedByUserName,
            Type = r.Type,
            Magnitude = r.Magnitude,
            Outcome = (SharedModels.CalibrationOutcome)r.Outcome,
            ObservedError = r.ObservedError,
            MaxPermissibleError = r.MaxPermissibleError,
            Uncertainty = r.Uncertainty,
            CertificatePath = r.CertificatePath,
            NextDueDate = r.NextDueDate,
            Restrictions = r.Restrictions,
            ImpactAssessmentRequired = r.ImpactAssessmentRequired,
            Notes = r.Notes,
            VolumeNominal = r.VolumeNominal,
            VolumeTested = r.VolumeTested,
            SystematicError = r.SystematicError,
            RandomError = r.RandomError,
            AcceptableLimit = r.AcceptableLimit,
            PointsResultsJson = r.PointsResultsJson
        }).ToList();

        var sharedRepairs = repairs.Select(r => new SharedModels.EquipmentRepairRecord
        {
            Id = r.Id,
            EquipmentId = r.EquipmentId,
            DetectionDate = r.DetectionDate,
            ProblemDescription = r.ProblemDescription,
            DetectedDuring = r.DetectedDuring,
            Severity = r.Severity,
            EquipmentRemovedFromService = r.EquipmentRemovedFromService,
            RemovedFromServiceDate = r.RemovedFromServiceDate,
            TechnicalServiceNotified = r.TechnicalServiceNotified,
            NotificationDate = r.NotificationDate,
            InterventionDate = r.InterventionDate,
            InterventionDescription = r.InterventionDescription,
            PartsReplaced = r.PartsReplaced,
            ConfigurationModified = r.ConfigurationModified,
            SoftwareUpdated = r.SoftwareUpdated,
            Outcome = r.Outcome,
            VerificationRequired = r.VerificationRequired,
            VerificationPerformed = r.VerificationPerformed,
            ReactivationDate = r.ReactivationDate,
            EndStatus = (SharedModels.EquipmentStatus)r.EndStatus,
            EvidencePath = r.EvidencePath,
            Notes = r.Notes,
            PerformedBy = r.PerformedBy
        }).ToList();

        var sharedIncidents = incidents.Select(i => new SharedModels.EquipmentIncident
        {
            Id = i.Id,
            EquipmentId = i.EquipmentId,
            IncidentDate = i.IncidentDate,
            IncidentType = i.IncidentType,
            Description = i.Description,
            Severity = i.Severity,
            ImmediateStatus = (SharedModels.EquipmentStatus)i.ImmediateStatus,
            ImmediateAction = i.ImmediateAction,
            RequiresRemoval = i.RequiresRemoval,
            RequiresRepair = i.RequiresRepair,
            RequiresImpactAssessment = i.RequiresImpactAssessment,
            RequiresNotification = i.RequiresNotification,
            EvidencePath = i.EvidencePath,
            IncidentStatus = i.IncidentStatus,
            ClosedAt = i.ClosedAt,
            Conclusion = i.Conclusion
        }).ToList();

        var sharedImpacts = impacts.Select(a => new SharedModels.EquipmentImpactAssessment
        {
            Id = a.Id,
            EquipmentId = a.EquipmentId,
            IncidentId = a.IncidentId,
            RepairId = a.RepairId,
            LastConformingVerificationDate = a.LastConformingVerificationDate,
            ProbableStartDateOfProblem = a.ProbableStartDateOfProblem,
            PotentiallyAffectedPeriod = a.PotentiallyAffectedPeriod,
            ImpactType = a.ImpactType,
            RequiresExternalReview = a.RequiresExternalReview,
            ExternalNCId = a.ExternalNCId,
            Decision = a.Decision,
            Justification = a.Justification,
            EndStatus = (SharedModels.EquipmentStatus)a.EndStatus,
            EvidencePath = a.EvidencePath,
            ClosedAt = a.ClosedAt
        }).ToList();

        var sharedDecommissions = decommissions.Select(d => new SharedModels.EquipmentDecommission
        {
            Id = d.Id,
            EquipmentId = d.EquipmentId,
            Type = d.Type,
            Reason = d.Reason,
            Date = d.Date,
            PreviousStatus = (SharedModels.EquipmentStatus)d.PreviousStatus,
            NewStatus = (SharedModels.EquipmentStatus)d.NewStatus,
            RequiresDecontamination = d.RequiresDecontamination,
            DecontaminationPerformed = d.DecontaminationPerformed,
            DecontaminationEvidencePath = d.DecontaminationEvidencePath,
            Destination = d.Destination,
            Notes = d.Notes,
            ValidatedByUserId = d.ValidatedByUserId,
            ValidatedByUserName = d.ValidatedByUserName
        }).ToList();

        var sharedAlerts = alerts.Select(a => new SharedModels.EquipmentAlert
        {
            Id = a.Id,
            EquipmentId = a.EquipmentId,
            EquipmentName = a.EquipmentName,
            Type = a.Type,
            Message = a.Message,
            Severity = a.Severity,
            CreatedAt = a.CreatedAt,
            IsActive = a.IsActive
        }).ToList();

        var sharedHistory = history.Select(h => new SharedModels.EquipmentHistory
        {
            Id = h.Id,
            EquipmentId = h.EquipmentId,
            Date = h.Date,
            UserId = h.UserId,
            UserName = h.UserName,
            ActionType = h.ActionType,
            Description = h.Description,
            OldValue = h.OldValue,
            NewValue = h.NewValue,
            Notes = h.Notes,
            EvidencePath = h.EvidencePath
        }).ToList();

        return new EquipmentDetailDto
        {
            Equipment = sharedEquipment,
            Acceptance = sharedAcceptance,
            MaintenancePlans = sharedMaintPlans,
            MaintenanceEvents = sharedMaintEvents,
            FunctionalQCs = sharedQcs,
            CalibrationPlans = sharedCalPlans,
            CalibrationRecords = sharedCalRecords,
            RepairRecords = sharedRepairs,
            Incidents = sharedIncidents,
            ImpactAssessments = sharedImpacts,
            Decommissions = sharedDecommissions,
            StatusHistory = sharedHistory.Where(h => h.ActionType == "STATUS_CHANGE").Select(h => new SharedModels.EquipmentStatusHistory
            {
                Id = h.Id,
                EquipmentId = h.EquipmentId,
                Date = h.Date,
                OldStatus = Enum.TryParse<SharedModels.EquipmentStatus>(h.OldValue ?? "", out var os) ? os : SharedModels.EquipmentStatus.PENDING_RECEIPT,
                NewStatus = Enum.TryParse<SharedModels.EquipmentStatus>(h.NewValue ?? "", out var ns) ? ns : SharedModels.EquipmentStatus.PENDING_RECEIPT,
                Reason = h.Description,
                UserId = h.UserId,
                UserName = h.UserName,
                EvidencePath = h.EvidencePath,
                Notes = h.Notes
            }).ToList(),
            ActiveAlerts = sharedAlerts,
            AuditHistory = sharedHistory
        };
    }

    public async Task<Guid> CreateEquipmentAsync(CreateEquipmentRequest request, Guid? userId = null, string? userName = null)
    {
        var e = new DomainEntities.Equipment
        {
            Id = Guid.NewGuid(),
            InternalId = request.InternalId,
            AssetTag = request.AssetTag,
            Name = request.Name,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            SerialNumber = request.SerialNumber,
            SoftwareVersion = request.SoftwareVersion,
            FirmwareVersion = request.FirmwareVersion,
            Location = request.Location,
            Status = DomainEntities.EquipmentStatus.PENDING_RECEIPT,
            Criticidad = (DomainEntities.EquipmentCriticidad)request.Criticidad,
            Aptitude = DomainEntities.EquipmentAptitude.NO_APTO,
            HospitalInventoryNumber = request.HospitalInventoryNumber,
            AreaLaboratorio = request.AreaLaboratorio,
            Description = request.Description,
            IntendedUse = request.IntendedUse,
            PurchaseDate = request.PurchaseDate,
            InstalledAt = request.InstalledAt,
            WarrantyUntil = request.WarrantyUntil,
            HasMaintenanceContract = request.HasMaintenanceContract,
            TechnicalServiceInfo = request.TechnicalServiceInfo,
            Notes = request.Notes,
            ImpactResult = request.ImpactResult,
            ImpactPreservation = request.ImpactPreservation,
            ImpactTraceability = request.ImpactTraceability,
            ImpactBiosecurity = request.ImpactBiosecurity,
            ImpactContinuity = request.ImpactContinuity,
            ImpactPreparation = request.ImpactPreparation,
            ImpactAnalysis = request.ImpactAnalysis,
            
            // Spec properties
            CytoType = request.CytoType,
            CytoLasersCount = request.CytoLasersCount,
            CytoWavelengths = request.CytoWavelengths,
            CytoDetectorsCount = request.CytoDetectorsCount,
            CytoOpticalConfig = request.CytoOpticalConfig,
            CytoFilters = request.CytoFilters,
            CytoParameters = request.CytoParameters,
            CytoAcquisitionSoftware = request.CytoAcquisitionSoftware,
            CytoAcquisitionSoftwareVersion = request.CytoAcquisitionSoftwareVersion,
            CytoAssociatedComputer = request.CytoAssociatedComputer,
            CytoOS = request.CytoOS,
            CytoAcquisitionConfig = request.CytoAcquisitionConfig,
            CytoQcConfig = request.CytoQcConfig,
            CytoFcsExport = request.CytoFcsExport,
            CytoFcsExportPath = request.CytoFcsExportPath,
            
            PipetteType = request.PipetteType,
            PipetteNominalVolume = request.PipetteNominalVolume,
            PipetteVolumeRange = request.PipetteVolumeRange,
            PipetteResolution = request.PipetteResolution,
            PipetteCriticalUse = request.PipetteCriticalUse,
            PipetteIntendedUse = request.PipetteIntendedUse,
            PipetteEmpLimit = request.PipetteEmpLimit,
            
            ColdTempMin = request.ColdTempMin,
            ColdTempMax = request.ColdTempMax,
            ColdTempRecordType = request.ColdTempRecordType,
            ColdSensorAssociated = request.ColdSensorAssociated,
            ColdHasAlarm = request.ColdHasAlarm,
            ColdHasBackup = request.ColdHasBackup,
            
            CentrifugeType = request.CentrifugeType,
            CentrifugeRotor = request.CentrifugeRotor,
            CentrifugeRpmRange = request.CentrifugeRpmRange,
            CentrifugeRcfRange = request.CentrifugeRcfRange,
            CentrifugeHasTimer = request.CentrifugeHasTimer,
            CentrifugeSpecificMaintenance = request.CentrifugeSpecificMaintenance,
            
            SoftName = request.SoftName,
            SoftManufacturer = request.SoftManufacturer,
            SoftVersion = request.SoftVersion,
            SoftLicense = request.SoftLicense,
            SoftComputerInstalled = request.SoftComputerInstalled,
            SoftFunction = request.SoftFunction,
            SoftInstallationDate = request.SoftInstallationDate,
            SoftValidationState = request.SoftValidationState
        };

        _context.Equipments.Add(e);
        await LogHistoryAsync(e.Id, userId ?? Guid.Empty, "CREATE", $"Equipo registrado preliminarmente: {e.Name} ({e.InternalId})");
        await LogAuditAsync("CREATE", "Equipment", e.Id, $"Equipo registrado preliminarmente: {e.Name} ({e.InternalId})", userId, userName);
        await _context.SaveChangesAsync();

        await RecalculateAlertsForEquipmentAsync(e.Id);
        return e.Id;
    }

    public async Task<bool> UpdateEquipmentAsync(Guid id, UpdateEquipmentRequest request, Guid? userId = null, string? userName = null)
    {
        var e = await _context.Equipments.FindAsync(id);
        if (e == null) return false;

        e.InternalId = request.InternalId;
        e.AssetTag = request.AssetTag;
        e.Name = request.Name;
        e.Manufacturer = request.Manufacturer;
        e.Model = request.Model;
        e.SerialNumber = request.SerialNumber;
        e.SoftwareVersion = request.SoftwareVersion;
        e.FirmwareVersion = request.FirmwareVersion;
        e.Location = request.Location;
        e.Criticidad = (DomainEntities.EquipmentCriticidad)request.Criticidad;
        e.HospitalInventoryNumber = request.HospitalInventoryNumber;
        e.AreaLaboratorio = request.AreaLaboratorio;
        e.Description = request.Description;
        e.IntendedUse = request.IntendedUse;
        e.PurchaseDate = request.PurchaseDate;
        e.InstalledAt = request.InstalledAt;
        e.WarrantyUntil = request.WarrantyUntil;
        e.HasMaintenanceContract = request.HasMaintenanceContract;
        e.TechnicalServiceInfo = request.TechnicalServiceInfo;
        e.Notes = request.Notes;
        e.ImpactResult = request.ImpactResult;
        e.ImpactPreservation = request.ImpactPreservation;
        e.ImpactTraceability = request.ImpactTraceability;
        e.ImpactBiosecurity = request.ImpactBiosecurity;
        e.ImpactContinuity = request.ImpactContinuity;
        e.ImpactPreparation = request.ImpactPreparation;
        e.ImpactAnalysis = request.ImpactAnalysis;

        e.CytoType = request.CytoType;
        e.CytoLasersCount = request.CytoLasersCount;
        e.CytoWavelengths = request.CytoWavelengths;
        e.CytoDetectorsCount = request.CytoDetectorsCount;
        e.CytoOpticalConfig = request.CytoOpticalConfig;
        e.CytoFilters = request.CytoFilters;
        e.CytoParameters = request.CytoParameters;
        e.CytoAcquisitionSoftware = request.CytoAcquisitionSoftware;
        e.CytoAcquisitionSoftwareVersion = request.CytoAcquisitionSoftwareVersion;
        e.CytoAssociatedComputer = request.CytoAssociatedComputer;
        e.CytoOS = request.CytoOS;
        e.CytoAcquisitionConfig = request.CytoAcquisitionConfig;
        e.CytoQcConfig = request.CytoQcConfig;
        e.CytoFcsExport = request.CytoFcsExport;
        e.CytoFcsExportPath = request.CytoFcsExportPath;

        e.PipetteType = request.PipetteType;
        e.PipetteNominalVolume = request.PipetteNominalVolume;
        e.PipetteVolumeRange = request.PipetteVolumeRange;
        e.PipetteResolution = request.PipetteResolution;
        e.PipetteCriticalUse = request.PipetteCriticalUse;
        e.PipetteIntendedUse = request.PipetteIntendedUse;
        e.PipetteEmpLimit = request.PipetteEmpLimit;

        e.ColdTempMin = request.ColdTempMin;
        e.ColdTempMax = request.ColdTempMax;
        e.ColdTempRecordType = request.ColdTempRecordType;
        e.ColdSensorAssociated = request.ColdSensorAssociated;
        e.ColdHasAlarm = request.ColdHasAlarm;
        e.ColdHasBackup = request.ColdHasBackup;

        e.CentrifugeType = request.CentrifugeType;
        e.CentrifugeRotor = request.CentrifugeRotor;
        e.CentrifugeRpmRange = request.CentrifugeRpmRange;
        e.CentrifugeRcfRange = request.CentrifugeRcfRange;
        e.CentrifugeHasTimer = request.CentrifugeHasTimer;
        e.CentrifugeSpecificMaintenance = request.CentrifugeSpecificMaintenance;

        e.SoftName = request.SoftName;
        e.SoftManufacturer = request.SoftManufacturer;
        e.SoftVersion = request.SoftVersion;
        e.SoftLicense = request.SoftLicense;
        e.SoftComputerInstalled = request.SoftComputerInstalled;
        e.SoftFunction = request.SoftFunction;
        e.SoftInstallationDate = request.SoftInstallationDate;
        e.SoftValidationState = request.SoftValidationState;

        // Validation rule checks if status overrides are requested
        var domainStatus = (DomainEntities.EquipmentStatus)request.Status;
        if (domainStatus != e.Status)
        {
            bool ok = await ValidateStatusChangeAsync(e, domainStatus);
            if (!ok) return false;
            
            var old = e.Status;
            e.Status = domainStatus;
            e.Aptitude = (DomainEntities.EquipmentAptitude)request.Aptitude;
            e.Restrictions = request.Restrictions;
            await LogHistoryAsync(e.Id, userId ?? Guid.Empty, "STATUS_CHANGE", $"Cambio manual de estado de {old} a {e.Status}", old.ToString(), e.Status.ToString(), request.Notes);
            await LogAuditAsync("STATUS_CHANGE", "Equipment", e.Id, $"Cambio manual de estado de {old} a {e.Status} (InternalId: {e.InternalId})", userId, userName);
        }
        else
        {
            await LogHistoryAsync(e.Id, userId ?? Guid.Empty, "EDIT", "Ficha técnica del equipo modificada");
            await LogAuditAsync("EDIT", "Equipment", e.Id, $"Ficha técnica del equipo modificada (InternalId: {e.InternalId})", userId, userName);
        }

        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(e.Id);
        return true;
    }

    public async Task<bool> DeleteEquipmentAsync(Guid id, Guid? userId = null, string? userName = null)
    {
        var e = await _context.Equipments.FindAsync(id);
        if (e == null) return false;

        e.IsDeleted = true;
        await LogHistoryAsync(e.Id, userId ?? Guid.Empty, "DELETE", $"Borrado lógico del equipo: {e.Name}");
        await LogAuditAsync("SOFT_DELETE", "Equipment", e.Id, $"Borrado lógico del equipo: {e.Name} (InternalId: {e.InternalId})", userId, userName);
        
        // Disable alerts
        var alerts = await _context.EquipmentAlerts.Where(a => a.EquipmentId == id).ToListAsync();
        foreach (var a in alerts) a.IsActive = false;

        await _context.SaveChangesAsync();
        return true;
    }

    // ── Acceptance Flow ──
    public async Task<bool> RegisterAcceptanceAsync(RegisterAcceptanceRequest request, Guid? userId = null, string? userName = null)
    {
        var e = await _context.Equipments.FindAsync(request.EquipmentId);
        if (e == null) return false;

        var acc = await _context.EquipmentAcceptances.FirstOrDefaultAsync(a => a.EquipmentId == request.EquipmentId);
        if (acc == null)
        {
            acc = new DomainEntities.EquipmentAcceptance { Id = Guid.NewGuid(), EquipmentId = request.EquipmentId };
            _context.EquipmentAcceptances.Add(acc);
        }

        acc.ReceptionDate = request.ReceptionDate;
        acc.ReceptionCondition = request.ReceptionCondition;
        acc.PackagingCorrect = request.PackagingCorrect;
        acc.VisualDamage = request.VisualDamage;
        acc.AccessoriesReceived = request.AccessoriesReceived;
        acc.ReceptionNotes = request.ReceptionNotes;
        acc.ReceptionEvidencePath = request.ReceptionEvidencePath;

        acc.InstallationDate = request.InstallationDate;
        acc.InstalledBy = request.InstalledBy;
        acc.AmbientConditionsCorrect = request.AmbientConditionsCorrect;
        acc.ConnectionsCorrect = request.ConnectionsCorrect;
        acc.InitialPowerOnCorrect = request.InitialPowerOnCorrect;
        acc.SoftwareCommunicationCorrect = request.SoftwareCommunicationCorrect;
        acc.InstallationNotes = request.InstallationNotes;
        acc.InstallationEvidencePath = request.InstallationEvidencePath;

        acc.AcceptanceDate = request.AcceptanceDate;
        acc.CriteriaDefined = request.CriteriaDefined;
        acc.CriteriaMet = request.CriteriaMet;
        acc.AcceptanceOutcome = request.AcceptanceOutcome;
        acc.AcceptanceRestrictions = request.AcceptanceRestrictions;
        acc.ServiceEntryDate = request.ServiceEntryDate;
        acc.AcceptanceEvidencePath = request.AcceptanceEvidencePath;
        acc.AcceptanceNotes = request.AcceptanceNotes;

        // Auto transition equipment status based on outcome
        var oldStatus = e.Status;
        if (request.AcceptanceOutcome == "Aceptado")
        {
            e.Status = DomainEntities.EquipmentStatus.ACCEPTED;
            e.Aptitude = DomainEntities.EquipmentAptitude.APTO;
            e.ReceptionDate = request.ReceptionDate;
            e.ReceptionCondition = request.ReceptionCondition;
            e.InstalledAt = request.InstallationDate;
            e.AcceptanceDate = request.AcceptanceDate;
            e.ServiceEntryDate = request.ServiceEntryDate;
        }
        else if (request.AcceptanceOutcome == "Aceptado con Restricciones")
        {
            e.Status = DomainEntities.EquipmentStatus.IN_SERVICE_WITH_RESTRICTIONS;
            e.Aptitude = DomainEntities.EquipmentAptitude.CON_RESTRICCIONES;
            e.Restrictions = request.AcceptanceRestrictions;
            e.ReceptionDate = request.ReceptionDate;
            e.ReceptionCondition = request.ReceptionCondition;
            e.InstalledAt = request.InstallationDate;
            e.AcceptanceDate = request.AcceptanceDate;
            e.ServiceEntryDate = request.ServiceEntryDate;
        }
        else if (request.AcceptanceOutcome == "Rechazado")
        {
            e.Status = DomainEntities.EquipmentStatus.IN_QUARANTINE;
            e.Aptitude = DomainEntities.EquipmentAptitude.NO_APTO;
            e.Restrictions = "Rechazado en aceptación inicial: " + request.AcceptanceNotes;
        }

        await LogHistoryAsync(e.Id, userId ?? Guid.Empty, "ACCEPTANCE", 
            $"Registro de recepción y aceptación inicial. Resultado: {request.AcceptanceOutcome}", 
            oldStatus.ToString(), e.Status.ToString(), request.AcceptanceNotes, request.AcceptanceEvidencePath);
        await LogAuditAsync("ACCEPTANCE", "Equipment", e.Id, $"Registro de recepción y aceptación inicial. Resultado: {request.AcceptanceOutcome} (InternalId: {e.InternalId})", userId, userName);

        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(e.Id);
        return true;
    }

    // ── Quality Control & Verificaciones ──
    public async Task<bool> RegisterQCAsync(CreateDailyQCRequest request)
    {
        var e = await _context.Equipments.FindAsync(request.EquipmentId);
        if (e == null) return false;

        var qc = new DomainEntities.EquipmentFunctionalQC
        {
            Id = Guid.NewGuid(),
            EquipmentId = request.EquipmentId,
            PerformedAt = request.PerformedAt,
            PerformedByUserId = request.UserId ?? Guid.Empty,
            PerformedByUserName = (await _context.Users.FindAsync(request.UserId ?? Guid.Empty))?.FullName ?? "Usuario",
            Type = (DomainEntities.QCType)request.Type,
            LotNumber = request.LotNumber,
            ParametersEvaluated = request.ParametersEvaluated,
            AcceptanceCriteria = request.AcceptanceCriteria,
            ObtainedValues = request.ObtainedValues,
            Outcome = request.IsPass ? DomainEntities.QCOutcome.CONFORME : DomainEntities.QCOutcome.NO_CONFORME,
            IsPass = request.IsPass,
            EvidencePath = request.EvidencePath,
            Notes = request.Notes,
            ActionTaken = request.ActionTaken,
            EquipmentEndStatus = (DomainEntities.EquipmentStatus)request.EquipmentEndStatus
        };

        _context.EquipmentFunctionalQC.Add(qc);

        var oldStatus = e.Status;
        // Rules validation: If QC fails, update equipment status
        if (!request.IsPass)
        {
            e.Status = qc.EquipmentEndStatus == DomainEntities.EquipmentStatus.IN_SERVICE 
                ? DomainEntities.EquipmentStatus.QC_NON_CONFORMING 
                : qc.EquipmentEndStatus;
            
            e.Aptitude = DomainEntities.EquipmentAptitude.NO_APTO;
            e.Restrictions = "QC Fallido: " + request.Notes;
        }
        else
        {
            // If it was QC non-conforming or pending verification, restore to service
            if (e.Status == DomainEntities.EquipmentStatus.QC_NON_CONFORMING || e.Status == DomainEntities.EquipmentStatus.PENDING_QC_VERIFICATION)
            {
                e.Status = DomainEntities.EquipmentStatus.IN_SERVICE;
                e.Aptitude = DomainEntities.EquipmentAptitude.APTO;
                e.Restrictions = null;
            }
        }

        e.VerificationDate = request.PerformedAt;
        e.IsVerified = request.IsPass;

        await LogHistoryAsync(e.Id, request.UserId ?? Guid.Empty, "QC_RECORD",
            $"Registro de Verificación Funcional ({qc.Type}). Resultado global: {(qc.IsPass ? "CONFORME" : "NO CONFORME")}",
            oldStatus.ToString(), e.Status.ToString(), request.Notes, request.EvidencePath);

        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(e.Id);
        return true;
    }

    // ── Preventative Maintenance ──
    public async Task<bool> RegisterMaintenanceAsync(RegisterMaintenanceRequest request)
    {
        var e = await _context.Equipments.FindAsync(request.EquipmentId);
        if (e == null) return false;

        var plan = request.PlanId.HasValue ? await _context.MaintenancePlans.FindAsync(request.PlanId.Value) : null;

        var evt = new DomainEntities.MaintenanceEvent
        {
            Id = Guid.NewGuid(),
            EquipmentId = request.EquipmentId,
            PlanId = request.PlanId,
            PlanName = request.PlanName ?? plan?.PlanName ?? "Mantenimiento Preventivo",
            ScheduledDate = request.ScheduledDate,
            PerformedAt = request.PerformedAt ?? DateTime.UtcNow,
            PerformedByUserId = request.UserId,
            PerformedByUserName = (await _context.Users.FindAsync(request.UserId ?? Guid.Empty))?.FullName ?? "Técnico",
            EventType = (DomainEntities.MaintenanceEventType)request.EventType,
            IsInternal = request.IsInternal,
            ActivitiesPerformed = request.ActivitiesPerformed,
            Outcome = request.Outcome,
            HasDeviation = request.HasDeviation,
            DeviationReason = request.DeviationReason,
            RequiresAdditionalAction = request.RequiresAdditionalAction,
            RequiresVerification = request.RequiresVerification,
            VerificationPerformed = request.VerificationPerformed,
            EndStatus = request.EndStatus.HasValue ? (DomainEntities.EquipmentStatus?)request.EndStatus.Value : null,
            Notes = request.Notes,
            CertificatePath = request.CertificatePath,
            Cost = request.Cost,
            IsEfficiencyCheck = request.IsEfficiencyCheck,
            HasIssues = request.Outcome == "No conforme"
        };

        // Recalculate next due date if linked to active plan
        if (plan != null && plan.IsActive)
        {
            plan.StartDate = request.PerformedAt ?? DateTime.UtcNow;
            plan.NextDueDate = (request.PerformedAt ?? DateTime.UtcNow).AddDays(plan.FrequencyDays);
        }

        _context.MaintenanceEvents.Add(evt);

        var oldStatus = e.Status;
        if (evt.EndStatus.HasValue)
        {
            e.Status = evt.EndStatus.Value;
        }

        await LogHistoryAsync(e.Id, request.UserId ?? Guid.Empty, "MAINTENANCE",
            $"Registro de Mantenimiento ({evt.PlanName}). Resultado: {evt.Outcome}",
            oldStatus.ToString(), e.Status.ToString(), request.Notes, request.CertificatePath);

        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(e.Id);
        return true;
    }

    public async Task<bool> RegisterMaintenancePlanAsync(RegisterMaintenancePlanRequest request)
    {
        var plan = new DomainEntities.MaintenancePlan
        {
            Id = Guid.NewGuid(),
            EquipmentId = request.EquipmentId,
            PlanName = request.PlanName,
            FrequencyDays = request.FrequencyDays,
            ChecklistJson = request.ChecklistJson ?? "{}",
            IsActive = request.IsActive,
            StartDate = request.StartDate,
            NextDueDate = request.NextDueDate ?? (request.StartDate.HasValue ? request.StartDate.Value.AddDays(request.FrequencyDays) : null),
            ToleranceDays = request.ToleranceDays,
            Responsible = request.Responsible,
            RequiresStop = request.RequiresStop,
            RequiresVerification = request.RequiresVerification
        };

        _context.MaintenancePlans.Add(plan);
        await LogHistoryAsync(request.EquipmentId, Guid.Empty, "MAINTENANCE_PLAN",
            $"Nuevo plan de mantenimiento preventivo creado: {plan.PlanName}");
        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(request.EquipmentId);
        return true;
    }

    public async Task<bool> UpdateMaintenancePlanAsync(Guid id, RegisterMaintenancePlanRequest request)
    {
        var plan = await _context.MaintenancePlans.FindAsync(id);
        if (plan == null) return false;

        plan.PlanName = request.PlanName;
        plan.FrequencyDays = request.FrequencyDays;
        plan.ChecklistJson = request.ChecklistJson ?? "{}";
        plan.IsActive = request.IsActive;
        plan.StartDate = request.StartDate;
        plan.NextDueDate = request.NextDueDate ?? (request.StartDate.HasValue ? request.StartDate.Value.AddDays(request.FrequencyDays) : null);
        plan.ToleranceDays = request.ToleranceDays;
        plan.Responsible = request.Responsible;
        plan.RequiresStop = request.RequiresStop;
        plan.RequiresVerification = request.RequiresVerification;

        await LogHistoryAsync(request.EquipmentId, Guid.Empty, "MAINTENANCE_PLAN_UPDATE",
            $"Plan de mantenimiento preventivo modificado: {plan.PlanName}");
        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(request.EquipmentId);
        return true;
    }

    public async Task<bool> UpdateMaintenanceEventAsync(Guid id, RegisterMaintenanceRequest request)
    {
        var evt = await _context.MaintenanceEvents.FindAsync(id);
        if (evt == null) return false;

        evt.PlanId = request.PlanId;
        evt.PlanName = request.PlanName ?? evt.PlanName;
        evt.ScheduledDate = request.ScheduledDate;
        evt.PerformedAt = request.PerformedAt ?? DateTime.UtcNow;
        evt.PerformedByUserId = request.UserId;
        evt.PerformedByUserName = (await _context.Users.FindAsync(request.UserId ?? Guid.Empty))?.FullName ?? evt.PerformedByUserName;
        evt.EventType = (DomainEntities.MaintenanceEventType)request.EventType;
        evt.IsInternal = request.IsInternal;
        evt.ActivitiesPerformed = request.ActivitiesPerformed;
        evt.Outcome = request.Outcome;
        evt.HasDeviation = request.HasDeviation;
        evt.DeviationReason = request.DeviationReason;
        evt.RequiresAdditionalAction = request.RequiresAdditionalAction;
        evt.RequiresVerification = request.RequiresVerification;
        evt.VerificationPerformed = request.VerificationPerformed;
        if (request.EndStatus.HasValue)
        {
            evt.EndStatus = (DomainEntities.EquipmentStatus?)request.EndStatus.Value;
        }
        evt.Notes = request.Notes;
        evt.CertificatePath = request.CertificatePath;
        evt.Cost = request.Cost;
        evt.IsEfficiencyCheck = request.IsEfficiencyCheck;
        evt.HasIssues = request.Outcome == "No conforme";

        if (request.PlanId.HasValue)
        {
            var plan = await _context.MaintenancePlans.FindAsync(request.PlanId.Value);
            if (plan != null && plan.IsActive)
            {
                plan.StartDate = request.PerformedAt ?? DateTime.UtcNow;
                plan.NextDueDate = (request.PerformedAt ?? DateTime.UtcNow).AddDays(plan.FrequencyDays);
            }
        }

        await LogHistoryAsync(request.EquipmentId, request.UserId ?? Guid.Empty, "MAINTENANCE_UPDATE",
            $"Registro de mantenimiento modificado ({evt.PlanName}). Resultado: {evt.Outcome}");
        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(request.EquipmentId);
        return true;
    }

    // ── Calibration & Metrology ──
    public async Task<bool> RegisterCalibrationPlanAsync(RegisterCalibrationPlanRequest request)
    {
        var plan = new DomainEntities.EquipmentCalibrationPlan
        {
            Id = Guid.NewGuid(),
            EquipmentId = request.EquipmentId,
            ControlledMagnitude = request.ControlledMagnitude,
            FrequencyMonths = request.FrequencyMonths,
            Tolerance = request.Tolerance,
            ProviderOrMethod = request.ProviderOrMethod,
            RequiresCertificate = request.RequiresCertificate,
            IsActive = true,
            Notes = request.Notes
        };

        _context.EquipmentCalibrationPlans.Add(plan);
        await LogHistoryAsync(request.EquipmentId, Guid.Empty, "CALIBRATION_PLAN",
            $"Nuevo plan de calibración metrológica creado para magnitud: {plan.ControlledMagnitude}");
        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(request.EquipmentId);
        return true;
    }

    public async Task<bool> RegisterCalibrationRecordAsync(RegisterCalibrationRecordRequest request)
    {
        var e = await _context.Equipments.FindAsync(request.EquipmentId);
        if (e == null) return false;

        var rec = new DomainEntities.EquipmentCalibrationRecord
        {
            Id = Guid.NewGuid(),
            EquipmentId = request.EquipmentId,
            PlanId = request.PlanId,
            PerformedAt = request.PerformedAt,
            PerformedByUserId = request.UserId ?? Guid.Empty,
            PerformedByUserName = (await _context.Users.FindAsync(request.UserId ?? Guid.Empty))?.FullName ?? "Metrólogo",
            Type = request.Type,
            Magnitude = request.Magnitude,
            Outcome = (DomainEntities.CalibrationOutcome)request.Outcome,
            ObservedError = request.ObservedError,
            MaxPermissibleError = request.MaxPermissibleError,
            Uncertainty = request.Uncertainty,
            CertificatePath = request.CertificatePath,
            NextDueDate = request.NextDueDate,
            Restrictions = request.Restrictions,
            ImpactAssessmentRequired = request.ImpactAssessmentRequired,
            Notes = request.Notes,
            
            VolumeNominal = request.VolumeNominal,
            VolumeTested = request.VolumeTested,
            SystematicError = request.SystematicError,
            RandomError = request.RandomError,
            AcceptableLimit = request.AcceptableLimit,
            PointsResultsJson = request.PointsResultsJson
        };

        // Update plan dates if linked to plan
        if (request.PlanId.HasValue)
        {
            var plan = await _context.EquipmentCalibrationPlans.FindAsync(request.PlanId.Value);
            if (plan != null)
            {
                plan.LastCalibrationDate = request.PerformedAt;
                plan.NextCalibrationDate = request.NextDueDate ?? request.PerformedAt.AddMonths(plan.FrequencyMonths);
            }
        }

        e.LastCalibration = request.PerformedAt;
        e.NextCalibration = request.NextDueDate;
        
        var oldStatus = e.Status;
        if (rec.Outcome == DomainEntities.CalibrationOutcome.NO_APTO)
        {
            e.Status = DomainEntities.EquipmentStatus.QC_NON_CONFORMING;
            e.Aptitude = DomainEntities.EquipmentAptitude.NO_APTO;
            e.Restrictions = $"Calibración metrológica NO CONFORME para magnitud {request.Magnitude}";
        }
        else if (rec.Outcome == DomainEntities.CalibrationOutcome.APTO_CON_RESTRICCIONES)
        {
            e.Status = DomainEntities.EquipmentStatus.IN_SERVICE_WITH_RESTRICTIONS;
            e.Aptitude = DomainEntities.EquipmentAptitude.CON_RESTRICCIONES;
            e.Restrictions = request.Restrictions ?? $"Calibración conforme con restricciones.";
        }
        else
        {
            if (e.Status == DomainEntities.EquipmentStatus.QC_NON_CONFORMING)
            {
                e.Status = DomainEntities.EquipmentStatus.IN_SERVICE;
                e.Aptitude = DomainEntities.EquipmentAptitude.APTO;
                e.Restrictions = null;
            }
        }

        _context.EquipmentCalibrationRecords.Add(rec);

        await LogHistoryAsync(e.Id, request.UserId ?? Guid.Empty, "CALIBRATION",
            $"Registro de calibración metrológica ({rec.Type}). Resultado: {rec.Outcome}",
            oldStatus.ToString(), e.Status.ToString(), request.Notes, request.CertificatePath);

        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(e.Id);
        return true;
    }

    public async Task<bool> UpdateCalibrationPlanAsync(Guid id, RegisterCalibrationPlanRequest request)
    {
        var plan = await _context.EquipmentCalibrationPlans.FindAsync(id);
        if (plan == null) return false;

        plan.ControlledMagnitude = request.ControlledMagnitude;
        plan.FrequencyMonths = request.FrequencyMonths;
        plan.Tolerance = request.Tolerance;
        plan.ProviderOrMethod = request.ProviderOrMethod;
        plan.RequiresCertificate = request.RequiresCertificate;
        plan.Notes = request.Notes;

        await LogHistoryAsync(request.EquipmentId, Guid.Empty, "CALIBRATION_PLAN_UPDATE",
            $"Plan de calibración metrológica modificado para magnitud: {plan.ControlledMagnitude}");
        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(request.EquipmentId);
        return true;
    }

    public async Task<bool> UpdateCalibrationRecordAsync(Guid id, RegisterCalibrationRecordRequest request)
    {
        var rec = await _context.EquipmentCalibrationRecords.FindAsync(id);
        if (rec == null) return false;

        var e = await _context.Equipments.FindAsync(request.EquipmentId);
        if (e == null) return false;

        rec.PlanId = request.PlanId;
        rec.PerformedAt = request.PerformedAt;
        rec.PerformedByUserId = request.UserId ?? Guid.Empty;
        rec.PerformedByUserName = (await _context.Users.FindAsync(request.UserId ?? Guid.Empty))?.FullName ?? rec.PerformedByUserName;
        rec.Type = request.Type;
        rec.Magnitude = request.Magnitude;
        rec.Outcome = (DomainEntities.CalibrationOutcome)request.Outcome;
        rec.ObservedError = request.ObservedError;
        rec.MaxPermissibleError = request.MaxPermissibleError;
        rec.Uncertainty = request.Uncertainty;
        rec.CertificatePath = request.CertificatePath;
        rec.NextDueDate = request.NextDueDate;
        rec.Restrictions = request.Restrictions;
        rec.ImpactAssessmentRequired = request.ImpactAssessmentRequired;
        rec.Notes = request.Notes;
        
        rec.VolumeNominal = request.VolumeNominal;
        rec.VolumeTested = request.VolumeTested;
        rec.SystematicError = request.SystematicError;
        rec.RandomError = request.RandomError;
        rec.AcceptableLimit = request.AcceptableLimit;
        rec.PointsResultsJson = request.PointsResultsJson;

        if (request.PlanId.HasValue)
        {
            var plan = await _context.EquipmentCalibrationPlans.FindAsync(request.PlanId.Value);
            if (plan != null)
            {
                plan.LastCalibrationDate = request.PerformedAt;
                plan.NextCalibrationDate = request.NextDueDate ?? request.PerformedAt.AddMonths(plan.FrequencyMonths);
            }
        }

        e.LastCalibration = request.PerformedAt;
        e.NextCalibration = request.NextDueDate;
        
        var oldStatus = e.Status;
        if (rec.Outcome == DomainEntities.CalibrationOutcome.NO_APTO)
        {
            e.Status = DomainEntities.EquipmentStatus.QC_NON_CONFORMING;
            e.Aptitude = DomainEntities.EquipmentAptitude.NO_APTO;
            e.Restrictions = $"Calibración metrológica NO CONFORME para magnitud {request.Magnitude}";
        }
        else if (rec.Outcome == DomainEntities.CalibrationOutcome.APTO_CON_RESTRICCIONES)
        {
            e.Status = DomainEntities.EquipmentStatus.IN_SERVICE_WITH_RESTRICTIONS;
            e.Aptitude = DomainEntities.EquipmentAptitude.CON_RESTRICCIONES;
            e.Restrictions = request.Restrictions ?? $"Calibración conforme con restricciones.";
        }
        else
        {
            if (e.Status == DomainEntities.EquipmentStatus.QC_NON_CONFORMING)
            {
                e.Status = DomainEntities.EquipmentStatus.IN_SERVICE;
                e.Aptitude = DomainEntities.EquipmentAptitude.APTO;
                e.Restrictions = null;
            }
        }

        await LogHistoryAsync(e.Id, request.UserId ?? Guid.Empty, "CALIBRATION_UPDATE",
            $"Registro de calibración metrológica modificado ({rec.Type}). Resultado: {rec.Outcome}",
            oldStatus.ToString(), e.Status.ToString(), request.Notes, request.CertificatePath);

        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(e.Id);
        return true;
    }

    // ── Correctives & Repairs ──
    public async Task<bool> RegisterRepairAsync(RegisterRepairRequest request)
    {
        var e = await _context.Equipments.FindAsync(request.EquipmentId);
        if (e == null) return false;

        var rec = new DomainEntities.EquipmentRepairRecord
        {
            Id = Guid.NewGuid(),
            EquipmentId = request.EquipmentId,
            DetectionDate = request.DetectionDate,
            ProblemDescription = request.ProblemDescription,
            DetectedDuring = request.DetectedDuring,
            Severity = request.Severity,
            EquipmentRemovedFromService = request.EquipmentRemovedFromService,
            RemovedFromServiceDate = request.RemovedFromServiceDate,
            TechnicalServiceNotified = request.TechnicalServiceNotified,
            NotificationDate = request.NotificationDate,
            InterventionDate = request.InterventionDate,
            InterventionDescription = request.InterventionDescription,
            PartsReplaced = request.PartsReplaced,
            ConfigurationModified = request.ConfigurationModified,
            SoftwareUpdated = request.SoftwareUpdated,
            Outcome = request.Outcome,
            VerificationRequired = request.VerificationRequired,
            VerificationPerformed = request.VerificationPerformed,
            ReactivationDate = request.ReactivationDate,
            EndStatus = (DomainEntities.EquipmentStatus)request.EndStatus,
            EvidencePath = request.EvidencePath,
            Notes = request.Notes,
            PerformedBy = request.PerformedBy
        };

        _context.EquipmentRepairRecords.Add(rec);

        var oldStatus = e.Status;
        if (request.VerificationRequired)
        {
            e.Status = DomainEntities.EquipmentStatus.PENDING_QC_VERIFICATION;
            e.Aptitude = DomainEntities.EquipmentAptitude.NO_APTO;
            e.Restrictions = "Pendiente de verificación funcional post-reparación.";
        }
        else
        {
            e.Status = rec.EndStatus;
            if (e.Status == DomainEntities.EquipmentStatus.IN_SERVICE)
            {
                e.Aptitude = DomainEntities.EquipmentAptitude.APTO;
                e.Restrictions = null;
            }
        }

        await LogHistoryAsync(e.Id, request.UserId ?? Guid.Empty, "REPAIR",
            $"Registro de correctivo/reparación técnica. Resultado: {rec.Outcome}",
            oldStatus.ToString(), e.Status.ToString(), request.Notes, request.EvidencePath);

        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(e.Id);
        return true;
    }

    // ── Technical Incidents ──
    public async Task<bool> RegisterIncidentAsync(RegisterIncidentRequest request)
    {
        var e = await _context.Equipments.FindAsync(request.EquipmentId);
        if (e == null) return false;

        var inc = new DomainEntities.EquipmentIncident
        {
            Id = Guid.NewGuid(),
            EquipmentId = request.EquipmentId,
            IncidentDate = request.IncidentDate,
            IncidentType = request.IncidentType,
            Description = request.Description,
            Severity = request.Severity,
            ImmediateStatus = (DomainEntities.EquipmentStatus)request.ImmediateStatus,
            ImmediateAction = request.ImmediateAction,
            RequiresRemoval = request.RequiresRemoval,
            RequiresRepair = request.RequiresRepair,
            RequiresImpactAssessment = request.RequiresImpactAssessment,
            RequiresNotification = request.RequiresNotification,
            EvidencePath = request.EvidencePath,
            IncidentStatus = "Abierta"
        };

        _context.EquipmentIncidents.Add(inc);

        var oldStatus = e.Status;
        e.Status = inc.ImmediateStatus;
        e.Aptitude = DomainEntities.EquipmentAptitude.NO_APTO;
        e.Restrictions = $"Incidencia técnica crítica abierta: {request.IncidentType}. {request.Description}";

        await LogHistoryAsync(e.Id, request.UserId ?? Guid.Empty, "INCIDENT",
            $"Reporte de incidencia técnica ({inc.IncidentType}). Severidad: {inc.Severity}",
            oldStatus.ToString(), e.Status.ToString(), request.Description, request.EvidencePath);

        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(e.Id);
        return true;
    }

    // ── Impact Assessment ──
    public async Task<bool> RegisterImpactAssessmentAsync(RegisterImpactRequest request)
    {
        var e = await _context.Equipments.FindAsync(request.EquipmentId);
        if (e == null) return false;

        var assessment = new DomainEntities.EquipmentImpactAssessment
        {
            Id = Guid.NewGuid(),
            EquipmentId = request.EquipmentId,
            IncidentId = request.IncidentId,
            RepairId = request.RepairId,
            LastConformingVerificationDate = request.LastConformingVerificationDate,
            ProbableStartDateOfProblem = request.ProbableStartDateOfProblem,
            PotentiallyAffectedPeriod = request.PotentiallyAffectedPeriod,
            ImpactType = request.ImpactType,
            RequiresExternalReview = request.RequiresExternalReview,
            ExternalNCId = request.ExternalNCId,
            Decision = request.Decision,
            Justification = request.Justification,
            EndStatus = (DomainEntities.EquipmentStatus)request.EndStatus,
            EvidencePath = request.EvidencePath,
            ClosedAt = DateTime.UtcNow
        };

        _context.EquipmentImpactAssessments.Add(assessment);

        if (request.IncidentId.HasValue)
        {
            var inc = await _context.EquipmentIncidents.FindAsync(request.IncidentId.Value);
            if (inc != null)
            {
                inc.IncidentStatus = "Cerrada";
                inc.ClosedAt = DateTime.UtcNow;
                inc.Conclusion = $"Evaluación de impacto finalizada. Decisión: {request.Decision}. Justificación: {request.Justification}";
            }
        }

        var oldStatus = e.Status;
        e.Status = assessment.EndStatus;
        if (e.Status == DomainEntities.EquipmentStatus.IN_SERVICE)
        {
            e.Aptitude = DomainEntities.EquipmentAptitude.APTO;
            e.Restrictions = null;
        }
        else if (e.Status == DomainEntities.EquipmentStatus.IN_SERVICE_WITH_RESTRICTIONS)
        {
            e.Aptitude = DomainEntities.EquipmentAptitude.CON_RESTRICCIONES;
            e.Restrictions = $"Estado condicionado por evaluación de impacto técnico: {request.Decision}";
        }

        await LogHistoryAsync(e.Id, request.UserId ?? Guid.Empty, "IMPACT_ASSESSMENT",
            $"Evaluación de impacto técnico registrada. Decisión final: {request.Decision}",
            oldStatus.ToString(), e.Status.ToString(), request.Justification, request.EvidencePath);

        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(e.Id);
        return true;
    }

    // ── Decommissioning ──
    public async Task<bool> RegisterDecommissionAsync(RegisterDecommissionRequest request)
    {
        var e = await _context.Equipments.FindAsync(request.EquipmentId);
        if (e == null) return false;

        var targetStatus = request.Type switch
        {
            "Cuarentena" => DomainEntities.EquipmentStatus.IN_QUARANTINE,
            "Fuera de servicio temporal" => DomainEntities.EquipmentStatus.OUT_OF_SERVICE,
            "Retirada" => DomainEntities.EquipmentStatus.RETIRED,
            "Obsoleto" => DomainEntities.EquipmentStatus.OBSOLETE,
            "Baja definitiva" => DomainEntities.EquipmentStatus.DECOMMISSIONED,
            _ => DomainEntities.EquipmentStatus.DECOMMISSIONED
        };

        var d = new DomainEntities.EquipmentDecommission
        {
            Id = Guid.NewGuid(),
            EquipmentId = request.EquipmentId,
            Type = request.Type,
            Reason = request.Reason,
            Date = request.Date,
            PreviousStatus = e.Status,
            NewStatus = targetStatus,
            RequiresDecontamination = request.RequiresDecontamination,
            DecontaminationPerformed = request.DecontaminationPerformed,
            DecontaminationEvidencePath = request.DecontaminationEvidencePath,
            Destination = request.Destination,
            Notes = request.Notes,
            ValidatedByUserId = request.UserId,
            ValidatedByUserName = (await _context.Users.FindAsync(request.UserId))?.FullName ?? "Administrador"
        };

        _context.EquipmentDecommissions.Add(d);

        var oldStatus = e.Status;
        e.Status = d.NewStatus;
        e.Aptitude = DomainEntities.EquipmentAptitude.NO_APTO;
        e.Restrictions = $"Retirado / Baja de servicio. Motivo: {request.Reason}";
        if (request.Type == "Baja definitiva" || request.Type == "Obsoleto")
        {
            e.DecommissionDate = request.Date;
        }

        await LogHistoryAsync(e.Id, request.UserId, "DECOMMISSION",
            $"Retirada/Cuarentena/Baja de equipo ({request.Type}). Destino: {request.Destination}",
            oldStatus.ToString(), e.Status.ToString(), request.Notes, request.DecontaminationEvidencePath);

        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(e.Id);
        return true;
    }

    // ── Status Manual Overrides ──
    public async Task<bool> UpdateEquipmentStatusAsync(Guid equipmentId, EquipmentStatus newStatus, string reason, Guid userId)
    {
        var e = await _context.Equipments.FindAsync(equipmentId);
        if (e == null) return false;

        var domainStatus = (DomainEntities.EquipmentStatus)newStatus;
        bool ok = await ValidateStatusChangeAsync(e, domainStatus);
        if (!ok) return false;

        var oldStatus = e.Status;
        e.Status = domainStatus;
        if (domainStatus == DomainEntities.EquipmentStatus.IN_SERVICE)
        {
            e.Aptitude = DomainEntities.EquipmentAptitude.APTO;
            e.Restrictions = null;
        }
        else if (domainStatus == DomainEntities.EquipmentStatus.IN_SERVICE_WITH_RESTRICTIONS)
        {
            e.Aptitude = DomainEntities.EquipmentAptitude.CON_RESTRICCIONES;
        }
        else
        {
            e.Aptitude = DomainEntities.EquipmentAptitude.NO_APTO;
        }

        await LogHistoryAsync(e.Id, userId, "STATUS_CHANGE",
            $"Cambio manual de estado: {reason}", oldStatus.ToString(), domainStatus.ToString());
        
        await _context.SaveChangesAsync();
        await RecalculateAlertsForEquipmentAsync(e.Id);
        return true;
    }

    // ── Validation Rules ──
    private async Task<bool> ValidateStatusChangeAsync(DomainEntities.Equipment e, DomainEntities.EquipmentStatus targetStatus)
    {
        if (targetStatus == DomainEntities.EquipmentStatus.IN_SERVICE || targetStatus == DomainEntities.EquipmentStatus.IN_SERVICE_WITH_RESTRICTIONS)
        {
            // Rule 1: Cannot activate critical equipment without initial acceptance
            if (e.Criticidad == DomainEntities.EquipmentCriticidad.CRITICAL)
            {
                var hasAcceptance = await _context.EquipmentAcceptances.AnyAsync(a => a.EquipmentId == e.Id && (a.AcceptanceOutcome == "Aceptado" || a.AcceptanceOutcome == "Aceptado con Restricciones"));
                if (!hasAcceptance)
                {
                    return false;
                }
            }

            // Rule 2: Cannot activate critical equipment after repair without verification
            if (e.Criticidad == DomainEntities.EquipmentCriticidad.CRITICAL)
            {
                var lastRepair = await _context.EquipmentRepairRecords
                    .Where(r => r.EquipmentId == e.Id)
                    .OrderByDescending(r => r.DetectionDate)
                    .FirstOrDefaultAsync();

                if (lastRepair != null && lastRepair.VerificationRequired && !lastRepair.VerificationPerformed)
                {
                    var hasPostRepairQC = await _context.EquipmentFunctionalQC
                        .AnyAsync(q => q.EquipmentId == e.Id && q.PerformedAt >= lastRepair.DetectionDate && q.IsPass);
                    
                    if (!hasPostRepairQC)
                    {
                        return false;
                    }
                    else
                    {
                        lastRepair.VerificationPerformed = true;
                    }
                }
            }
        }

        return true;
    }

    // ── Dashboard Statistics & Active Alerts ──
    public async Task<EquipmentDashboardDto> GetDashboardStatsAsync()
    {
        var list = await _context.Equipments
            .Include(eq => eq.MaintenanceEvents)
            .ToListAsync();

        var alerts = await _context.EquipmentAlerts.Where(a => a.IsActive).ToListAsync();
        var calRecords = await _context.EquipmentCalibrationRecords.ToListAsync();
        var repairs = await _context.EquipmentRepairRecords.ToListAsync();
        var incidents = await _context.EquipmentIncidents.ToListAsync();

        int total = list.Count;
        int critical = list.Count(e => e.Criticidad == DomainEntities.EquipmentCriticidad.CRITICAL);
        int active = list.Count(e => e.Status == DomainEntities.EquipmentStatus.IN_SERVICE || e.Status == DomainEntities.EquipmentStatus.IN_SERVICE_WITH_RESTRICTIONS);
        int outOfService = list.Count(e => e.Status == DomainEntities.EquipmentStatus.OUT_OF_SERVICE || e.Status == DomainEntities.EquipmentStatus.IN_QUARANTINE || e.Status == DomainEntities.EquipmentStatus.DECOMMISSIONED);

        double activePct = total > 0 ? (double)active / total * 100 : 0;
        double oosPct = total > 0 ? (double)outOfService / total * 100 : 0;

        int totalMaint = 0;
        int onTimeMaint = 0;
        foreach (var evt in list.SelectMany(e => e.MaintenanceEvents))
        {
            totalMaint++;
            if (!evt.HasDeviation) onTimeMaint++;
        }
        double maintInTime = totalMaint > 0 ? (double)onTimeMaint / totalMaint * 100 : 100;

        int totalCal = calRecords.Count;
        int onTimeCal = calRecords.Count(r => r.Outcome != DomainEntities.CalibrationOutcome.NO_APTO);
        double calInTime = totalCal > 0 ? (double)onTimeCal / totalCal * 100 : 100;

        var cytometers = list.Where(e => e.CytoType != null || e.Name.Contains("Citómetro", StringComparison.OrdinalIgnoreCase)).ToList();
        int cytoCount = cytometers.Count;
        int cytoActive = cytometers.Count(c => c.Status == DomainEntities.EquipmentStatus.IN_SERVICE || c.Status == DomainEntities.EquipmentStatus.IN_SERVICE_WITH_RESTRICTIONS);
        double cytoAvailability = cytoCount > 0 ? (double)cytoActive / cytoCount * 100 : 100;

        int cytoDowntime = 0;
        var now = DateTime.UtcNow;
        foreach (var cyto in cytometers)
        {
            var repairsForCyto = repairs.Where(r => r.EquipmentId == cyto.Id && r.RemovedFromServiceDate.HasValue).ToList();
            foreach (var r in repairsForCyto)
            {
                var end = r.ReactivationDate ?? now;
                var diff = (end - r.RemovedFromServiceDate!.Value).TotalDays;
                cytoDowntime += (int)diff;
            }
        }

        var sharedAlerts = alerts.Select(a => new SharedModels.EquipmentAlert
        {
            Id = a.Id,
            EquipmentId = a.EquipmentId,
            EquipmentName = a.EquipmentName,
            Type = a.Type,
            Message = a.Message,
            Severity = a.Severity,
            CreatedAt = a.CreatedAt,
            IsActive = a.IsActive
        }).ToList();

        return new EquipmentDashboardDto
        {
            TotalEquipments = total,
            CriticalEquipments = critical,
            ActivePercentage = activePct,
            OutOfServicePercentage = oosPct,
            MaintenanceInTimePercentage = maintInTime,
            CalibrationInTimePercentage = calInTime,
            ActiveIncidentsCount = incidents.Count(i => i.IncidentStatus == "Abierta" || i.IncidentStatus == "En investigación"),
            PendingAcceptanceCount = list.Count(e => e.Status == DomainEntities.EquipmentStatus.PENDING_ACCEPTANCE),
            PendingVerificationPostRepairCount = list.Count(e => e.Status == DomainEntities.EquipmentStatus.PENDING_QC_VERIFICATION),
            CytometersAvailability = cytoAvailability,
            CytometersDowntimeDays = cytoDowntime,
            ActiveAlerts = sharedAlerts
        };
    }

    public async Task<List<SharedModels.EquipmentHistory>> GetEquipmentHistoryAsync(Guid equipmentId)
    {
        var history = await _context.EquipmentHistory
            .Where(h => h.EquipmentId == equipmentId)
            .OrderByDescending(h => h.Date)
            .ToListAsync();

        return history.Select(h => new SharedModels.EquipmentHistory
        {
            Id = h.Id,
            EquipmentId = h.EquipmentId,
            Date = h.Date,
            UserId = h.UserId,
            UserName = h.UserName,
            ActionType = h.ActionType,
            Description = h.Description,
            OldValue = h.OldValue,
            NewValue = h.NewValue,
            Notes = h.Notes,
            EvidencePath = h.EvidencePath
        }).ToList();
    }

    // ── Generate Alerts ──
    private async Task RecalculateAlertsForEquipmentAsync(Guid equipmentId)
    {
        var e = await _context.Equipments
            .AsNoTracking()
            .Include(eq => eq.MaintenancePlans)
            .Include(eq => eq.MaintenanceEvents)
            .FirstOrDefaultAsync(eq => eq.Id == equipmentId);

        if (e == null) return;

        var existing = await _context.EquipmentAlerts.Where(a => a.EquipmentId == equipmentId && a.IsActive).ToListAsync();
        foreach (var alert in existing) alert.IsActive = false;

        var now = DateTime.UtcNow;

        if (e.Status == DomainEntities.EquipmentStatus.PENDING_RECEIPT || e.Status == DomainEntities.EquipmentStatus.RECEIVED || e.Status == DomainEntities.EquipmentStatus.PENDING_ACCEPTANCE)
        {
            _context.EquipmentAlerts.Add(new DomainEntities.EquipmentAlert
            {
                Id = Guid.NewGuid(),
                EquipmentId = e.Id,
                EquipmentName = e.Name,
                Type = "PENDING_ACCEPTANCE",
                Message = $"Equipo '{e.Name}' pendiente de recepción o aceptación de servicio clínico.",
                Severity = "WARNING",
                CreatedAt = now,
                IsActive = true
            });
        }

        if (e.Criticidad == DomainEntities.EquipmentCriticidad.CRITICAL)
        {
            if (e.MaintenancePlans.Count == 0)
            {
                _context.EquipmentAlerts.Add(new DomainEntities.EquipmentAlert
                {
                    Id = Guid.NewGuid(),
                    EquipmentId = e.Id,
                    EquipmentName = e.Name,
                    Type = "CRITICAL_NO_MAINTENANCE",
                    Message = $"Equipo crítico '{e.Name}' no tiene definido ningún plan de mantenimiento preventivo.",
                    Severity = "CRITICAL",
                    CreatedAt = now,
                    IsActive = true
                });
            }

            var hasCalPlan = await _context.EquipmentCalibrationPlans.AnyAsync(p => p.EquipmentId == e.Id && p.IsActive);
            if (!hasCalPlan && (e.ImpactResult || e.ImpactTraceability || e.PipetteNominalVolume.HasValue || e.ColdTempMin.HasValue || e.CentrifugeRpmRange != null))
            {
                _context.EquipmentAlerts.Add(new DomainEntities.EquipmentAlert
                {
                    Id = Guid.NewGuid(),
                    EquipmentId = e.Id,
                    EquipmentName = e.Name,
                    Type = "CRITICAL_NO_CALIBRATION",
                    Message = $"Equipo crítico '{e.Name}' no tiene definido ningún plan de calibración metrológica.",
                    Severity = "CRITICAL",
                    CreatedAt = now,
                    IsActive = true
                });
            }
        }

        foreach (var p in e.MaintenancePlans.Where(p => p.IsActive && p.NextDueDate.HasValue))
        {
            var limitDate = p.NextDueDate!.Value;
            if (now > limitDate)
            {
                _context.EquipmentAlerts.Add(new DomainEntities.EquipmentAlert
                {
                    Id = Guid.NewGuid(),
                    EquipmentId = e.Id,
                    EquipmentName = e.Name,
                    Type = "MAINTENANCE_OVERDUE",
                    Message = $"Mantenimiento preventivo '{p.PlanName}' vencido el {limitDate:dd/MM/yyyy}.",
                    Severity = "CRITICAL",
                    CreatedAt = now,
                    IsActive = true
                });
            }
            else if ((limitDate - now).TotalDays <= 7)
            {
                _context.EquipmentAlerts.Add(new DomainEntities.EquipmentAlert
                {
                    Id = Guid.NewGuid(),
                    EquipmentId = e.Id,
                    EquipmentName = e.Name,
                    Type = "MAINTENANCE_DUE",
                    Message = $"Mantenimiento preventivo '{p.PlanName}' vence en {(int)(limitDate - now).TotalDays} días ({limitDate:dd/MM/yyyy}).",
                    Severity = "WARNING",
                    CreatedAt = now,
                    IsActive = true
                });
            }
        }

        var calPlansForEq = await _context.EquipmentCalibrationPlans.Where(p => p.EquipmentId == e.Id && p.IsActive).ToListAsync();
        foreach (var cp in calPlansForEq.Where(cp => cp.NextCalibrationDate.HasValue))
        {
            var limitDate = cp.NextCalibrationDate!.Value;
            if (now > limitDate)
            {
                _context.EquipmentAlerts.Add(new DomainEntities.EquipmentAlert
                {
                    Id = Guid.NewGuid(),
                    EquipmentId = e.Id,
                    EquipmentName = e.Name,
                    Type = "CALIBRATION_OVERDUE",
                    Message = $"Calibración metrológica para magnitud '{cp.ControlledMagnitude}' vencida el {limitDate:dd/MM/yyyy}.",
                    Severity = e.Criticidad == DomainEntities.EquipmentCriticidad.CRITICAL ? "CRITICAL" : "WARNING",
                    CreatedAt = now,
                    IsActive = true
                });
            }
            else if ((limitDate - now).TotalDays <= 30)
            {
                _context.EquipmentAlerts.Add(new DomainEntities.EquipmentAlert
                {
                    Id = Guid.NewGuid(),
                    EquipmentId = e.Id,
                    EquipmentName = e.Name,
                    Type = "CALIBRATION_DUE",
                    Message = $"Calibración metrológica para magnitud '{cp.ControlledMagnitude}' vence en {(int)(limitDate - now).TotalDays} días ({limitDate:dd/MM/yyyy}).",
                    Severity = "WARNING",
                    CreatedAt = now,
                    IsActive = true
                });
            }
        }

        var lastQC = await _context.EquipmentFunctionalQC.Where(qc => qc.EquipmentId == e.Id).OrderByDescending(qc => qc.PerformedAt).FirstOrDefaultAsync();
        if (lastQC != null)
        {
            if (!lastQC.IsPass)
            {
                _context.EquipmentAlerts.Add(new DomainEntities.EquipmentAlert
                {
                    Id = Guid.NewGuid(),
                    EquipmentId = e.Id,
                    EquipmentName = e.Name,
                    Type = "QC_FAIL",
                    Message = $"Última verificación funcional registrada no fue conforme: {lastQC.Notes}.",
                    Severity = "CRITICAL",
                    CreatedAt = now,
                    IsActive = true
                });
            }
        }

        if (e.Status == DomainEntities.EquipmentStatus.PENDING_QC_VERIFICATION)
        {
            _context.EquipmentAlerts.Add(new DomainEntities.EquipmentAlert
            {
                Id = Guid.NewGuid(),
                EquipmentId = e.Id,
                EquipmentName = e.Name,
                Type = "PENDING_POST_REPAIR_VERIFICATION",
                Message = $"Equipo '{e.Name}' requiere verificación funcional post-reparación antes de reactivar.",
                Severity = "CRITICAL",
                CreatedAt = now,
                IsActive = true
            });
        }

        if (e.Status == DomainEntities.EquipmentStatus.OUT_OF_SERVICE)
        {
            _context.EquipmentAlerts.Add(new DomainEntities.EquipmentAlert
            {
                Id = Guid.NewGuid(),
                EquipmentId = e.Id,
                EquipmentName = e.Name,
                Type = "OUT_OF_SERVICE",
                Message = $"El equipo '{e.Name}' está FUERA DE SERVICIO por avería o restricciones.",
                Severity = "CRITICAL",
                CreatedAt = now,
                IsActive = true
            });
        }
        else if (e.Status == DomainEntities.EquipmentStatus.IN_QUARANTINE)
        {
            _context.EquipmentAlerts.Add(new DomainEntities.EquipmentAlert
            {
                Id = Guid.NewGuid(),
                EquipmentId = e.Id,
                EquipmentName = e.Name,
                Type = "IN_QUARANTINE",
                Message = $"El equipo '{e.Name}' está en CUARENTENA.",
                Severity = "CRITICAL",
                CreatedAt = now,
                IsActive = true
            });
        }

        if (e.Status == DomainEntities.EquipmentStatus.IN_SERVICE_WITH_RESTRICTIONS)
        {
            _context.EquipmentAlerts.Add(new DomainEntities.EquipmentAlert
            {
                Id = Guid.NewGuid(),
                EquipmentId = e.Id,
                EquipmentName = e.Name,
                Type = "RESTRICTED",
                Message = $"El equipo '{e.Name}' está funcionando CON RESTRICCIONES: {e.Restrictions}.",
                Severity = "WARNING",
                CreatedAt = now,
                IsActive = true
            });
        }

        await _context.SaveChangesAsync();
    }
}
