using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Application.Services.Inventory;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Infrastructure.Services.Inventory;

public class InventoryService : IInventoryService
{
    private readonly QmsDbContext _context;

    public InventoryService(QmsDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ReagentListDto>> GetReagentsAsync(bool? isActive = null, bool? isLowStock = null)
    {
        var query = _context.Reagents
            .Include(r => r.Supplier)
            .Include(r => r.Lots)
            .AsQueryable();

        if (isActive.HasValue)
        {
            query = isActive.Value
                ? query.Where(r => r.Status == ReagentStatus.ACTIVO)
                : query.Where(r => r.Status != ReagentStatus.ACTIVO);
        }

        var list = await query.OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Manufacturer,
                r.Reference,
                r.ReagentType,
                r.Classification,
                r.Status,
                SupplierName = r.Supplier != null ? r.Supplier.Name : null,
                r.MinStock,
                r.TargetStock,
                r.ReorderQty,
                r.Fluorescence,
                r.InternalCode,
                Lots = r.Lots.Select(l => new
                {
                    l.Id,
                    l.LotNumber,
                    l.ExpiryDate,
                    l.AvailableQty,
                    l.Status
                }).ToList()
            })
            .ToListAsync();

        var dtos = list.Select(r =>
        {
            var activeLots = r.Lots.Where(l => l.AvailableQty > 0 && l.Status != LotStatus.CONSUMED).ToList();
            var totalStock = activeLots.Sum(l => l.AvailableQty);
            var nearestExpiry = activeLots.Any() ? activeLots.Min(l => l.ExpiryDate) : (DateTime?)null;

            int expiryStatus = 0; // OK
            if (nearestExpiry.HasValue)
            {
                var days = (nearestExpiry.Value.Date - DateTime.UtcNow.Date).TotalDays;
                if (days < 0) expiryStatus = 2; // Expired
                else if (days < 60) expiryStatus = 1; // Warning
            }

            return new ReagentListDto
            {
                Id = r.Id,
                Name = r.Name,
                Manufacturer = r.Manufacturer,
                Reference = r.Reference,
                ReagentType = r.ReagentType,
                Classification = r.Classification,
                Status = (QMSFlowDoc.Shared.Models.ReagentStatus)r.Status,
                SupplierName = r.SupplierName,
                TotalStock = totalStock,
                MinStock = r.MinStock,
                TargetStock = r.TargetStock,
                ReorderQty = r.ReorderQty,
                Fluorescence = r.Fluorescence,
                InternalCode = r.InternalCode,
                NearestExpiry = nearestExpiry,
                ExpiryStatus = expiryStatus,
                AvailableLots = activeLots.Select(l => new LotSummaryDto
                {
                    Id = l.Id,
                    LotNumber = l.LotNumber,
                    ExpiryDate = l.ExpiryDate,
                    Qty = l.AvailableQty
                }).ToList()
            };
        });

        if (isLowStock == true)
        {
            dtos = dtos.Where(r => r.TotalStock < r.MinStock);
        }

        return dtos.ToList();
    }

    public async Task<Reagent?> GetReagentByIdAsync(Guid id)
    {
        return await _context.Reagents
            .Include(r => r.Supplier)
            .Include(r => r.DefaultLocation)
            .Include(r => r.Lots.OrderByDescending(l => l.CreatedAt))
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Reagent?> CreateReagentAsync(CreateReagentRequest request)
    {
        var reagent = new Reagent
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Manufacturer = request.Manufacturer,
            ReagentType = request.ReagentType,
            Reference = request.Reference,
            Classification = request.Classification,
            StorageConditions = request.StorageConditions,
            OpenShelfLifeDays = request.OpenShelfLifeDays,
            MinStock = request.MinStock,
            TargetStock = request.TargetStock,
            ReorderQty = request.ReorderQty,
            Status = ReagentStatus.ACTIVO,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Fluorescence = request.Fluorescence ?? "",
            ManufacturerCode = request.ManufacturerCode,
            InternalCode = request.InternalCode,
            SupplierId = request.SupplierId,
            DefaultLocationId = request.DefaultLocationId
        };

        _context.Reagents.Add(reagent);
        await LogAuditAsync("CREATE", "Reagent", reagent.Id, $"Reactivo creado: {reagent.Name}", request.SupplierId, "Sistema");
        await _context.SaveChangesAsync();
        return reagent;
    }

    public async Task<bool> UpdateReagentAsync(Guid id, CreateReagentRequest request)
    {
        var r = await _context.Reagents.FindAsync(id);
        if (r == null) return false;

        r.Name = request.Name;
        r.Manufacturer = request.Manufacturer;
        r.ReagentType = request.ReagentType;
        r.Reference = request.Reference;
        r.Classification = request.Classification;
        r.StorageConditions = request.StorageConditions;
        r.OpenShelfLifeDays = request.OpenShelfLifeDays;
        r.MinStock = request.MinStock;
        r.TargetStock = request.TargetStock;
        r.ReorderQty = request.ReorderQty;
        r.Fluorescence = request.Fluorescence ?? "";
        r.ManufacturerCode = request.ManufacturerCode;
        r.InternalCode = request.InternalCode;
        r.SupplierId = request.SupplierId;
        r.DefaultLocationId = request.DefaultLocationId;
        r.UpdatedAt = DateTime.UtcNow;

        await LogAuditAsync("EDIT", "Reagent", r.Id, $"Reactivo actualizado: {r.Name}", null, "Sistema");
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateReagentStatusAsync(Guid id, int status)
    {
        var r = await _context.Reagents.FindAsync(id);
        if (r == null) return false;

        var oldStatus = r.Status;
        r.Status = (ReagentStatus)status;
        r.UpdatedAt = DateTime.UtcNow;

        await LogAuditAsync("STATUS_CHANGE", "Reagent", r.Id, $"Estado cambiado de {oldStatus} a {r.Status}", null, "Sistema");
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<List<ReagentLot>?> RegisterLotAsync(RegisterLotRequest request)
    {
        // Check if there is an existing active lot with same LotNumber and ExpiryDate for this Reagent
        var existingLot = await _context.ReagentLots
            .FirstOrDefaultAsync(l => l.ReagentId == request.ReagentId 
                                   && l.LotNumber == request.LotNumber 
                                   && l.ExpiryDate == request.ExpiryDate);

        if (existingLot != null)
        {
            existingLot.ReceivedQty += request.ReceivedQty;
            existingLot.AvailableQty += request.ReceivedQty;
            
            // If the lot was previously consumed, reactivate it in quarantine for validation
            if (existingLot.Status == LotStatus.CONSUMED)
            {
                existingLot.Status = LotStatus.QUARANTINE;
            }

            var movement = new InventoryMovement
            {
                Id = Guid.NewGuid(),
                ReagentId = request.ReagentId,
                ReagentLotId = existingLot.Id,
                Qty = request.ReceivedQty,
                MovementType = InventoryMovementType.IN,
                Reason = $"Lote {request.LotNumber} adicionado (Fusión con lote existente)",
                MovedAt = DateTime.UtcNow
            };
            _context.InventoryMovements.Add(movement);

            await LogAuditAsync("REGISTER_LOT_ADDITION", "ReagentLot", existingLot.Id, $"Adición de stock a lote existente: {existingLot.LotNumber} (+{request.ReceivedQty} uds.)", request.UserId, "Sistema");
            await _context.SaveChangesAsync();
        }
        else
        {
            var lot = new ReagentLot
            {
                Id = Guid.NewGuid(),
                ReagentId = request.ReagentId,
                LotNumber = request.LotNumber,
                ReceivedQty = request.ReceivedQty,
                AvailableQty = request.ReceivedQty,
                ExpiryDate = request.ExpiryDate,
                ReceivedDate = request.ReceivedDate,
                LocationId = request.LocationId,
                Status = LotStatus.QUARANTINE,
                CreatedAt = DateTime.UtcNow,
                PanelId = request.PanelId
            };
            _context.ReagentLots.Add(lot);
 
            var movement = new InventoryMovement
            {
                Id = Guid.NewGuid(),
                ReagentId = request.ReagentId,
                ReagentLotId = lot.Id,
                Qty = request.ReceivedQty,
                MovementType = InventoryMovementType.IN,
                Reason = $"Lote {request.LotNumber} registrado en cuarentena - pendiente de liberación",
                MovedAt = DateTime.UtcNow
            };
            _context.InventoryMovements.Add(movement);
 
            await LogAuditAsync("REGISTER_LOT", "ReagentLot", lot.Id, $"Lote registrado en cuarentena: {lot.LotNumber} para reactivo {request.ReagentId}", request.UserId, "Sistema");
            await _context.SaveChangesAsync();
        }

        return await _context.ReagentLots
            .Where(l => l.ReagentId == request.ReagentId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> AdjustStockAsync(AdjustStockRequest request)
    {
        var lot = await _context.ReagentLots.FindAsync(request.ReagentLotId);
        if (lot == null) return false;

        // Validaciones estrictas ISO 15189 para salidas de stock (consumo)
        if (request.Qty < 0)
        {
            var consumableStatuses = new[] { LotStatus.RELEASED, LotStatus.IN_USE };
            if (!consumableStatuses.Contains(lot.Status))
            {
                throw new InvalidOperationException($"No se permite el consumo del lote {lot.LotNumber} porque su estado es {lot.Status}. Debe estar Liberado o En Uso.");
            }

            if (Math.Abs(request.Qty) > lot.AvailableQty)
            {
                throw new InvalidOperationException($"Stock insuficiente en el lote {lot.LotNumber}. Disponible: {lot.AvailableQty}, Solicitado: {Math.Abs(request.Qty)}.");
            }

            if (lot.ExpiryDate < DateTime.UtcNow)
            {
                throw new InvalidOperationException($"El lote {lot.LotNumber} está caducado (fecha de caducidad: {lot.ExpiryDate:dd/MM/yyyy}). No se puede utilizar en clínica.");
            }
        }

        lot.AvailableQty += request.Qty;
        if (lot.AvailableQty <= 0)
        {
            lot.AvailableQty = 0;
            lot.Status = LotStatus.CONSUMED;
        }

        var movement = new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ReagentId = lot.ReagentId,
            ReagentLotId = lot.Id,
            Qty = request.Qty,
            MovementType = (InventoryMovementType)request.MovementType,
            Reason = request.Reason,
            MovedAt = DateTime.UtcNow,
            Notes = request.Notes
        };
        _context.InventoryMovements.Add(movement);

        await LogAuditAsync("ADJUST_STOCK", "ReagentLot", lot.Id, $"Stock ajustado en {request.Qty} unidades ({request.MovementType}). Motivo: {request.Reason}", request.UserId, "Sistema");
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> ReleaseLotAsync(ReleaseLotRequest request)
    {
        var lot = await _context.ReagentLots.FindAsync(request.LotId);
        if (lot == null) return false;

        if (lot.Status != LotStatus.QUARANTINE)
        {
            throw new InvalidOperationException($"El lote {lot.LotNumber} no está en cuarentena (estado actual: {lot.Status}).");
        }

        lot.Status = LotStatus.RELEASED;
        lot.ReleaseByUserId = request.UserId;
        lot.ReleaseAt = DateTime.UtcNow;

        var movement = new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ReagentId = lot.ReagentId,
            ReagentLotId = lot.Id,
            Qty = 0,
            MovementType = InventoryMovementType.ADJUST,
            Reason = $"Liberación de cuarentena. Criterios: {request.AcceptanceCriteria}",
            MovedAt = DateTime.UtcNow,
            Notes = request.Notes
        };
        _context.InventoryMovements.Add(movement);

        await LogAuditAsync("RELEASE_LOT", "ReagentLot", lot.Id, $"Lote {lot.LotNumber} liberado de cuarentena. Criterios: {request.AcceptanceCriteria}", request.UserId, request.UserName);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateLotStatusAsync(Guid lotId, LotStatus newStatus, Guid? userId, string username)
    {
        var lot = await _context.ReagentLots.FindAsync(lotId);
        if (lot == null) return false;

        var oldStatus = lot.Status;
        lot.Status = newStatus;

        if (newStatus == LotStatus.QUARANTINE)
        {
            lot.ReleaseByUserId = null;
            lot.ReleaseAt = null;
        }

        var movement = new InventoryMovement
        {
            Id = Guid.NewGuid(),
            ReagentId = lot.ReagentId,
            ReagentLotId = lot.Id,
            Qty = 0,
            MovementType = InventoryMovementType.ADJUST,
            Reason = $"Estado de lote cambiado de {oldStatus} a {newStatus}",
            MovedAt = DateTime.UtcNow
        };
        _context.InventoryMovements.Add(movement);

        await LogAuditAsync("LOT_STATUS_CHANGE", "ReagentLot", lot.Id, $"Estado de lote cambiado de {oldStatus} a {newStatus}", userId, username);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteReagentAsync(Guid id)
    {
        var r = await _context.Reagents.FindAsync(id);
        if (r == null) return false;

        r.IsDeleted = true;
        r.UpdatedAt = DateTime.UtcNow;

        await LogAuditAsync("DELETE", "Reagent", r.Id, $"Reactivo borrado de forma lógica: {r.Name}", null, "Sistema");
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<List<InventoryMovementDto>> GetMovementsAsync(
        DateTime? from, DateTime? to, InventoryMovementType? type, Guid? reagentId)
    {
        var query = _context.InventoryMovements
            .Include(m => m.Reagent)
            .Include(m => m.ReagentLot)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(m => m.MovedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(m => m.MovedAt <= to.Value);
        if (type.HasValue)
            query = query.Where(m => m.MovementType == type.Value);
        if (reagentId.HasValue)
            query = query.Where(m => m.ReagentId == reagentId.Value);

        return await query
            .OrderByDescending(m => m.MovedAt)
            .Take(500)
            .Select(m => new InventoryMovementDto(
                m.Id,
                m.MovedAt,
                "Sistema", 
                m.Reagent != null ? m.Reagent.Name : "?",
                m.Reagent != null ? m.Reagent.Manufacturer : null,
                m.Reagent != null ? m.Reagent.Fluorescence : null,
                m.MovementType.ToString(),
                m.Qty,
                m.ReagentLot != null ? m.ReagentLot.LotNumber : null,
                m.ReagentLot != null ? m.ReagentLot.ExpiryDate : null,
                m.Reason ?? ""
            ))
            .ToListAsync();
    }

    public async Task<IEnumerable<StorageLocation>> GetStorageLocationsAsync()
    {
        return await _context.StorageLocations.OrderBy(l => l.Name).ToListAsync();
    }

    public async Task<IEnumerable<Supplier>> GetSuppliersAsync()
    {
        await SyncSuppliersFromLatestEvaluationsAsync();
        return await _context.Suppliers.Where(s => !s.IsDeleted).OrderBy(s => s.Name).ToListAsync();
    }

    private async Task SyncSuppliersFromLatestEvaluationsAsync()
    {
        var suppliers = await _context.Suppliers.Where(s => !s.IsDeleted).ToListAsync();
        if (!suppliers.Any()) return;

        var supplierIds = suppliers.Select(s => s.Id).ToList();
        var evaluations = await _context.SupplierEvaluations
            .Where(e => supplierIds.Contains(e.SupplierId))
            .ToListAsync();

        var bySupplierId = evaluations
            .GroupBy(e => e.SupplierId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(e => e.EvaluationDate).ThenByDescending(e => e.CreatedAt).First());
        var hasChanges = false;
        foreach (var supplier in suppliers)
        {
            if (!bySupplierId.TryGetValue(supplier.Id, out var evaluation))
            {
                if (supplier.LastEvaluationDate.HasValue ||
                    supplier.NextEvaluationDate.HasValue ||
                    supplier.QualityStatus != SupplierQualityStatus.PENDIENTE)
                {
                    supplier.LastEvaluationDate = null;
                    supplier.NextEvaluationDate = null;
                    supplier.QualityStatus = SupplierQualityStatus.PENDIENTE;
                    supplier.UpdatedAt = DateTime.UtcNow;
                    hasChanges = true;
                }
                continue;
            }

            var status = MapSupplierDecisionToQualityStatus(evaluation.Decision);
            if (supplier.LastEvaluationDate != evaluation.EvaluationDate ||
                supplier.NextEvaluationDate != evaluation.NextEvaluationDate ||
                supplier.QualityStatus != status)
            {
                supplier.LastEvaluationDate = evaluation.EvaluationDate;
                supplier.NextEvaluationDate = evaluation.NextEvaluationDate;
                supplier.QualityStatus = status;
                supplier.UpdatedAt = DateTime.UtcNow;
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Supplier?> CreateSupplierAsync(Supplier supplier)
    {
        supplier.Name = supplier.Name.Trim();
        if (string.IsNullOrWhiteSpace(supplier.Name))
        {
            throw new InvalidOperationException("El nombre del proveedor es obligatorio.");
        }

        var duplicate = await _context.Suppliers
            .AnyAsync(s => !s.IsDeleted && s.Name.ToLower() == supplier.Name.ToLower());
        if (duplicate)
        {
            throw new InvalidOperationException($"Ya existe un proveedor activo con el nombre '{supplier.Name}'.");
        }

        supplier.Id = supplier.Id == Guid.Empty ? Guid.NewGuid() : supplier.Id;
        supplier.ContactName = NormalizeText(supplier.ContactName);
        supplier.Email = NormalizeText(supplier.Email);
        supplier.Phone = NormalizeText(supplier.Phone);
        supplier.Address = NormalizeText(supplier.Address);
        supplier.Notes = NormalizeText(supplier.Notes);
        supplier.CreatedAt = DateTime.UtcNow;
        supplier.UpdatedAt = DateTime.UtcNow;
        supplier.IsDeleted = false;
        supplier.QualityStatus = SupplierQualityStatus.PENDIENTE;
        supplier.LastEvaluationDate = null;
        supplier.NextEvaluationDate = null;

        _context.Suppliers.Add(supplier);
        await LogAuditAsync("CREATE", "Supplier", supplier.Id, $"Proveedor creado: {supplier.Name}", null, "Sistema");
        await _context.SaveChangesAsync();
        return supplier;
    }

    public async Task<bool> UpdateSupplierAsync(Supplier supplier)
    {
        supplier.Name = supplier.Name.Trim();
        if (string.IsNullOrWhiteSpace(supplier.Name))
        {
            throw new InvalidOperationException("El nombre del proveedor es obligatorio.");
        }

        var existing = await _context.Suppliers.FindAsync(supplier.Id);
        if (existing == null || existing.IsDeleted)
        {
            return false;
        }

        var duplicate = await _context.Suppliers
            .AnyAsync(s => !s.IsDeleted && s.Id != supplier.Id && s.Name.ToLower() == supplier.Name.ToLower());
        if (duplicate)
        {
            throw new InvalidOperationException($"Ya existe otro proveedor activo con el nombre '{supplier.Name}'.");
        }

        existing.Name = supplier.Name;
        existing.ContactName = NormalizeText(supplier.ContactName);
        existing.Email = NormalizeText(supplier.Email);
        existing.Phone = NormalizeText(supplier.Phone);
        existing.Address = NormalizeText(supplier.Address);
        existing.Notes = NormalizeText(supplier.Notes);
        existing.Type = supplier.Type;
        existing.UpdatedAt = DateTime.UtcNow;

        await LogAuditAsync("EDIT", "Supplier", existing.Id, $"Proveedor actualizado: {existing.Name}", null, "Sistema");
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteSupplierAsync(Guid id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null || supplier.IsDeleted)
        {
            return false;
        }

        supplier.IsDeleted = true;
        supplier.UpdatedAt = DateTime.UtcNow;

        await LogAuditAsync("DELETE", "Supplier", supplier.Id, $"Proveedor borrado de forma lógica: {supplier.Name}", null, "Sistema");
        return await _context.SaveChangesAsync() > 0;
    }

    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SupplierQualityStatus MapSupplierDecisionToQualityStatus(string? decision) => decision switch
    {
        "Aprobado" => SupplierQualityStatus.APTO,
        "Aprobado con restricciones" => SupplierQualityStatus.EN_OBSERVACION,
        "Reevaluar" => SupplierQualityStatus.EVALUACION_CADUCADA,
        "No aprobado" => SupplierQualityStatus.NO_APTO,
        _ => SupplierQualityStatus.PENDIENTE
    };

    private async Task LogAuditAsync(string action, string entityType, Guid? entityId, string details, Guid? userId, string username)
    {
        var audit = new AuditLog
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
}
