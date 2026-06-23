using System;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public class ReagentListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string ReagentType { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public ReagentStatus Status { get; set; }
    public decimal TotalStock { get; set; }
    public decimal MinStock { get; set; }
    public decimal TargetStock { get; set; }
    public decimal ReorderQty { get; set; }
    public string? Fluorescence { get; set; }
    public string? InternalCode { get; set; }
    public string? Classification { get; set; }
    public string? SupplierName { get; set; }
    public List<LotSummaryDto> AvailableLots { get; set; } = new();

    public string LotFechaSummary => string.Join(" ", AvailableLots.Select(l => l.FormattedString));

    public DateTime? NearestExpiry { get; set; }
    
    // 0 = OK, 1 = Warning (< 60 days), 2 = Expired
    public int ExpiryStatus { get; set; } 

    public ReagentListDto() { }
    public ReagentListDto(Guid id, string name, string? manu, string? code, string? fluorescence, string type, string reference, ReagentStatus status, decimal stock, decimal min, decimal target)
    {
        Id = id; Name = name; Manufacturer = manu; InternalCode = code; Fluorescence = fluorescence;
        ReagentType = type; Reference = reference; Status = status; TotalStock = stock; MinStock = min; TargetStock = target;
    }

    public override string ToString()
    {
        var parts = new List<string> { Name };
        if (!string.IsNullOrWhiteSpace(Fluorescence)) parts.Add($"[{Fluorescence}]");
        if (!string.IsNullOrWhiteSpace(InternalCode)) parts.Add($"Cod:{InternalCode}");
        if (!string.IsNullOrWhiteSpace(Manufacturer)) parts.Add($"Fab:{Manufacturer}");
        return string.Join(" - ", parts);
    }
}

public class LotSummaryDto
{
    public Guid Id { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public decimal Qty { get; set; }
    public string FormattedString => $"LOTE:{LotNumber} FECHA:{(ExpiryDate.HasValue ? ExpiryDate.Value.ToString("MM/yy") : "N/A")} CANT:{Qty}";
}

public record CreateReagentRequest(
    string Name,
    string? Manufacturer,
    Guid? SupplierId,
    string? ManufacturerCode,
    string? InternalCode,
    string? Fluorescence,
    string ReagentType,
    string Reference,
    string? Classification, // ISO 15189
    string? StorageConditions,
    Guid? DefaultLocationId,
    int? OpenShelfLifeDays,
    decimal MinStock,
    decimal TargetStock,
    decimal ReorderQty
);

public record RegisterLotRequest(
    Guid ReagentId,
    string LotNumber,
    DateTime ExpiryDate,
    DateTime ReceivedDate,
    decimal ReceivedQty,
    Guid? LocationId,
    Guid? PanelId, // ISO 15189
    Guid? UserId // Added for local tracking
);

public record AdjustStockRequest(
    Guid ReagentId,
    Guid? ReagentLotId,
    InventoryMovementType MovementType,
    decimal Qty,
    string Reason,
    string? Notes = null,
    Guid? UserId = null // Added for local tracking
);


public record InventoryMovementDto(
    Guid Id,
    DateTime MovedAt,
    string UserName,
    string ReagentName,
    string? Manufacturer,
    string? Fluorescence,
    string AdjustmentType, // IN, OUT, etc.
    decimal Qty,
    string? LotNumber,
    DateTime? ExpiryDate,
    string Reason
);

// ==================== SUPPLIER DTOs (ISO 15189:2022 Section 6.8) ====================

public class SupplierListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SupplierType Type { get; set; }
    public SupplierQualityStatus QualityStatus { get; set; }
    public DateTime? LastEvaluationDate { get; set; }
    public DateTime? NextEvaluationDate { get; set; }
    public int EvaluationCount { get; set; }
    public int IncidentCount { get; set; }
    
    public SupplierListDto() { }
    public SupplierListDto(Guid id, string name, SupplierType type, SupplierQualityStatus status, 
        DateTime? lastEval, DateTime? nextEval, int evalCount, int incidentCount)
    {
        Id = id; Name = name; Type = type; QualityStatus = status;
        LastEvaluationDate = lastEval; NextEvaluationDate = nextEval;
        EvaluationCount = evalCount; IncidentCount = incidentCount;
    }
}

public class SupplierDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public SupplierType Type { get; set; }
    public SupplierQualityStatus QualityStatus { get; set; }
    public DateTime? LastEvaluationDate { get; set; }
    public DateTime? NextEvaluationDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<SupplierEvaluationDto> Evaluations { get; set; } = new();
}

public class SupplierEvaluationDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime EvaluationDate { get; set; }
    public string? EvaluatorName { get; set; }
    public string EvaluatedPeriod { get; set; } = string.Empty;
    public int ScorePlazos { get; set; }
    public int ScoreCalidad { get; set; }
    public int ScoreServicio { get; set; }
    public int ScoreIncidencias { get; set; }
    public double AverageScore => (ScorePlazos + ScoreCalidad + ScoreServicio + ScoreIncidencias) / 4.0;
    public bool IsApproved { get; set; }
    public string? Observations { get; set; }
    public string? AttachmentPath { get; set; }
}

public record CreateSupplierRequest(
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    string? Notes,
    SupplierType Type
);

public record CreateSupplierEvaluationRequest(
    Guid SupplierId,
    DateTime EvaluationDate,
    string EvaluatedPeriod,
    int ScorePlazos,
    int ScoreCalidad,
    int ScoreServicio,
    int ScoreIncidencias,
    bool IsApproved,
    string? Observations,
    string? AttachmentPath
);

public record ReleaseLotRequest(
    Guid LotId,
    Guid UserId,
    string UserName,
    string AcceptanceCriteria,
    string? CertificateOfAnalysis = null,
    string? ReceptionConditions = null,
    string? Notes = null
);

public class ActiveReagentDto
{
    public Guid ReagentId { get; set; }
    public string ReagentName { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? InternalCode { get; set; }
    public string? Fluorescence { get; set; }
    public string ReagentType { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string? Classification { get; set; }
    public string? SupplierName { get; set; }

    // Datos del lote activo
    public Guid? LotId { get; set; }
    public string LotNumber { get; set; } = "No disponible";
    public DateTime? ExpiryDate { get; set; }
    public decimal AvailableQty { get; set; }
    public decimal HistoricalQty { get; set; }
    
    // Información del último consumo
    public DateTime? LastConsumedDate { get; set; }
    public decimal LastConsumedQty { get; set; }
}


