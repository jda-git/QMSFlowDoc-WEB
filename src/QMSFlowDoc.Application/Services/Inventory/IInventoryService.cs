using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QMSFlowDoc.Application.Services.Inventory;

public interface IInventoryService
{
    Task<IEnumerable<ReagentListDto>> GetReagentsAsync(bool? isActive = null, bool? isLowStock = null);
    Task<Reagent?> GetReagentByIdAsync(Guid id);
    Task<Reagent?> CreateReagentAsync(CreateReagentRequest request);
    Task<bool> UpdateReagentAsync(Guid id, CreateReagentRequest request);
    Task<bool> UpdateReagentStatusAsync(Guid id, int status);
    Task<List<ReagentLot>?> RegisterLotAsync(RegisterLotRequest request);
    Task<bool> AdjustStockAsync(AdjustStockRequest request);
    Task<bool> UpdateLotStatusAsync(Guid lotId, QMSFlowDoc.Domain.Entities.LotStatus newStatus, Guid? userId, string username);
    Task<bool> ReleaseLotAsync(ReleaseLotRequest request);
    Task<bool> DeleteReagentAsync(Guid id);
    Task<List<InventoryMovementDto>> GetMovementsAsync(DateTime? from, DateTime? to, QMSFlowDoc.Domain.Entities.InventoryMovementType? type, Guid? reagentId);
    Task<IEnumerable<StorageLocation>> GetStorageLocationsAsync();
    Task<IEnumerable<Supplier>> GetSuppliersAsync();
}
