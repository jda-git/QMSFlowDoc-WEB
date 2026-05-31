using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Application.Services.Equipment;

public interface IEquipmentService
{
    // Inventory CRUD
    Task<List<EquipmentListDto>> GetEquipmentsAsync();
    Task<EquipmentDetailDto?> GetEquipmentDetailsAsync(Guid id);
    Task<Guid> CreateEquipmentAsync(CreateEquipmentRequest request, Guid? userId = null, string? userName = null);
    Task<bool> UpdateEquipmentAsync(Guid id, UpdateEquipmentRequest request, Guid? userId = null, string? userName = null);
    Task<bool> DeleteEquipmentAsync(Guid id, Guid? userId = null, string? userName = null);

    // Initial acceptance flow
    Task<bool> RegisterAcceptanceAsync(RegisterAcceptanceRequest request, Guid? userId = null, string? userName = null);

    // Quality Control & Daily checks
    Task<bool> RegisterQCAsync(CreateDailyQCRequest request);

    // Preventive Maintenance
    Task<bool> RegisterMaintenanceAsync(RegisterMaintenanceRequest request);
    Task<bool> RegisterMaintenancePlanAsync(RegisterMaintenancePlanRequest request);
    Task<bool> UpdateMaintenancePlanAsync(Guid id, RegisterMaintenancePlanRequest request);
    Task<bool> UpdateMaintenanceEventAsync(Guid id, RegisterMaintenanceRequest request);

    // Calibration & Metrology
    Task<bool> RegisterCalibrationPlanAsync(RegisterCalibrationPlanRequest request);
    Task<bool> RegisterCalibrationRecordAsync(RegisterCalibrationRecordRequest request);
    Task<bool> UpdateCalibrationPlanAsync(Guid id, RegisterCalibrationPlanRequest request);
    Task<bool> UpdateCalibrationRecordAsync(Guid id, RegisterCalibrationRecordRequest request);

    // Corrective & Repairs
    Task<bool> RegisterRepairAsync(RegisterRepairRequest request);

    // Technical Incidents
    Task<bool> RegisterIncidentAsync(RegisterIncidentRequest request);

    // Impact Assessment
    Task<bool> RegisterImpactAssessmentAsync(RegisterImpactRequest request);

    // Decommissioning, Quarantine & Baja
    Task<bool> RegisterDecommissionAsync(RegisterDecommissionRequest request);

    // Status overrides & overrides history
    Task<bool> UpdateEquipmentStatusAsync(Guid equipmentId, EquipmentStatus newStatus, string reason, Guid userId);

    // Dashboard Statistics & Active Alerts
    Task<EquipmentDashboardDto> GetDashboardStatsAsync();
    Task<List<EquipmentHistory>> GetEquipmentHistoryAsync(Guid equipmentId);
}
