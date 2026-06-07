using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IEquipmentService
{
    Task<IEnumerable<EquipmentListDto>> GetEquipmentAsync();
    Task<Equipment?> GetEquipmentByIdAsync(Guid id);
    Task<Equipment?> CreateEquipmentAsync(CreateEquipmentRequest request);
    Task<Equipment?> UpdateEquipmentAsync(UpdateEquipmentRequest request);
    Task<MaintenanceEvent?> GetLastMaintenanceAsync(Guid equipmentId);
    Task<MaintenanceEvent?> RegisterMaintenanceAsync(RegisterMaintenanceRequest request);
    Task<MaintenanceEvent?> UpdateMaintenanceAsync(UpdateMaintenanceRequest request);
    Task<bool> DeleteEquipmentAsync(Guid id);
    Task<bool> RegisterDailyQCAsync(CreateDailyQCRequest request);
    Task<IEnumerable<EquipmentDailyQCDto>> GetDailyQCAsync(Guid equipmentId);
}

/// <summary>
/// V2: Equipment service using SQL Server via EF Core.
/// All operations use short-lived DbContext for multi-user safety.
/// </summary>
public class EquipmentService : IEquipmentService
{
    private readonly ClientDbContextFactory _dbFactory;

    public EquipmentService(ClientDbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IEnumerable<EquipmentListDto>> GetEquipmentAsync()
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.Equipments
            .OrderBy(e => e.Name)
            .Select(e => new EquipmentListDto
            {
                Id = e.Id,
                Name = e.Name,
                Model = e.Model,
                Location = e.Location,
                Status = e.Status,
                InternalId = e.InternalId,
                AssetTag = e.AssetTag,
                NextCalibration = e.NextCalibration,
                IsVerified = e.IsVerified,
                CytoType = e.CytoType,
                PipetteType = e.PipetteType,
                ColdTempMin = e.ColdTempMin,
                CentrifugeType = e.CentrifugeType,
                SoftName = e.SoftName
            })
            .ToListAsync();
    }

    public async Task<Equipment?> GetEquipmentByIdAsync(Guid id)
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.Equipments
            .Include(e => e.MaintenancePlans)
            .Include(e => e.MaintenanceEvents.OrderByDescending(ev => ev.PerformedAt))
            .Include(e => e.FunctionalQCs.OrderByDescending(qc => qc.PerformedAt).Take(30))
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Equipment?> CreateEquipmentAsync(CreateEquipmentRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var equipment = new Equipment
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            SerialNumber = request.SerialNumber,
            SoftwareVersion = request.SoftwareVersion,
            FirmwareVersion = request.FirmwareVersion,
            Location = request.Location,
            InternalId = request.InternalId,
            AssetTag = request.AssetTag,
            InstalledAt = request.InstalledAt,
            Status = EquipmentStatus.ACTIVE
        };
        ctx.Equipments.Add(equipment);
        await ctx.SaveChangesAsync();
        return equipment;
    }

    public async Task<Equipment?> UpdateEquipmentAsync(UpdateEquipmentRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var eq = await ctx.Equipments.FindAsync(request.Id);
        if (eq == null) return null;

        eq.Name = request.Name;
        eq.Manufacturer = request.Manufacturer ?? eq.Manufacturer;
        eq.Model = request.Model ?? eq.Model;
        eq.SerialNumber = request.SerialNumber ?? eq.SerialNumber;
        eq.SoftwareVersion = request.SoftwareVersion ?? eq.SoftwareVersion;
        eq.FirmwareVersion = request.FirmwareVersion ?? eq.FirmwareVersion;
        eq.Location = request.Location ?? eq.Location;
        eq.InternalId = request.InternalId ?? eq.InternalId;
        eq.AssetTag = request.AssetTag ?? eq.AssetTag;
        eq.IsVerified = request.IsVerified;
        eq.NextCalibration = request.NextCalibration ?? eq.NextCalibration;

        await ctx.SaveChangesAsync();
        return eq;
    }

    public async Task<MaintenanceEvent?> GetLastMaintenanceAsync(Guid equipmentId)
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.MaintenanceEvents
            .Where(e => e.EquipmentId == equipmentId)
            .OrderByDescending(e => e.PerformedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<MaintenanceEvent?> RegisterMaintenanceAsync(RegisterMaintenanceRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var evt = new MaintenanceEvent
        {
            Id = Guid.NewGuid(),
            EquipmentId = request.EquipmentId,
            EventType = request.EventType,
            PerformedAt = request.PerformedAt ?? DateTime.UtcNow,
            PerformedByUserId = request.UserId,
            Notes = request.Notes,
            Cost = request.Cost,
            CertificatePath = request.CertificatePath,
            Outcome = request.Outcome
        };
        ctx.MaintenanceEvents.Add(evt);
        await ctx.SaveChangesAsync();
        return evt;
    }

    public async Task<MaintenanceEvent?> UpdateMaintenanceAsync(UpdateMaintenanceRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var evt = await ctx.MaintenanceEvents.FindAsync(request.Id);
        if (evt == null) return null;

        evt.EventType = request.EventType;
        evt.PerformedAt = request.PerformedAt ?? evt.PerformedAt;
        evt.Notes = request.Notes ?? evt.Notes;
        evt.Outcome = request.Outcome ?? evt.Outcome;
        evt.Cost = request.Cost ?? evt.Cost;
        evt.CertificatePath = request.CertificatePath ?? evt.CertificatePath;

        await ctx.SaveChangesAsync();
        return evt;
    }

    public async Task<bool> DeleteEquipmentAsync(Guid id)
    {
        using var ctx = _dbFactory.CreateContext();
        var eq = await ctx.Equipments.FindAsync(id);
        if (eq == null) return false;

        // Soft delete via IsDeleted flag
        eq.IsDeleted = true;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RegisterDailyQCAsync(CreateDailyQCRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var qc = new EquipmentFunctionalQC
        {
            Id = Guid.NewGuid(),
            EquipmentId = request.EquipmentId,
            PerformedAt = request.PerformedAt,
            PerformedByUserId = request.UserId ?? Guid.Empty,
            Notes = request.Notes,
            LotNumber = request.LotNumber,
            IsPass = request.IsPass
        };
        ctx.EquipmentFunctionalQC.Add(qc);
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<EquipmentDailyQCDto>> GetDailyQCAsync(Guid equipmentId)
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.EquipmentFunctionalQC
            .Where(qc => qc.EquipmentId == equipmentId)
            .OrderByDescending(qc => qc.PerformedAt)
            .Take(100)
            .Select(qc => new EquipmentDailyQCDto(qc.Id, qc.EquipmentId, qc.LotNumber, qc.IsPass, qc.Notes, qc.PerformedAt, ""))
            .ToListAsync();
    }
}
