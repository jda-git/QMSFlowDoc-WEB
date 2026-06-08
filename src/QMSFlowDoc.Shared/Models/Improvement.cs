using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Shared.Models;

public enum RiskLikelihood
{
    RARE = 1,
    UNLIKELY = 2,
    POSSIBLE = 3,
    LIKELY = 4,
    ALMOST_CERTAIN = 5
}

public enum RiskImpact
{
    INSIGNIFICANT = 1,
    MINOR = 2,
    MODERATE = 3,
    MAJOR = 4,
    CATASTROPHIC = 5
}

public enum RiskStatus
{
    ACTIVE,
    MITIGATED,
    ACCEPTED,
    RETIRED
}

public enum AuditStatus
{
    PLANNED,
    IN_PROGRESS,
    COMPLETED,
    CANCELLED
}

public enum FindingType
{
    OBSERVATION,
    MINOR_NC,
    MAJOR_NC,
    OPPORTUNITY_FOR_IMPROVEMENT
}

public class Risk
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "OPERATIONAL";
    public RiskLikelihood Likelihood { get; set; }
    public RiskImpact Impact { get; set; }
    public int RiskScore => (int)Likelihood * (int)Impact;
    public string? MitigationPlan { get; set; }
    public string? Opportunity { get; set; }
    public string? ActionPlan { get; set; }
    public string? Responsible { get; set; }
    public DateTime? DueDate { get; set; }
    public RiskLikelihood? ResidualLikelihood { get; set; }
    public RiskImpact? ResidualImpact { get; set; }
    public int? ResidualRiskScore => ResidualLikelihood.HasValue && ResidualImpact.HasValue ? (int)ResidualLikelihood.Value * (int)ResidualImpact.Value : null;
    public string? EffectivenessReview { get; set; }
    public DateTime? EffectivenessReviewDate { get; set; }
    public Guid? EvidenceDocumentId { get; set; }
    public Document? EvidenceDocument { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalNotes { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public User? Owner { get; set; }
    public RiskStatus Status { get; set; } = RiskStatus.ACTIVE;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class AuditPlan
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string? Objectives { get; set; }
    public string? Criteria { get; set; }
    public string? LeadAuditor { get; set; }
    public AuditStatus Status { get; set; } = AuditStatus.PLANNED;
    public string? SummaryReport { get; set; }
    public string? Conclusions { get; set; }
    public string? FollowUpActions { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProgramYear { get; set; }
    public string? InternalAuditors { get; set; }
    public string? AuditorIndependenceStatement { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalNotes { get; set; }
    
    public Guid? ReportDocumentId { get; set; }
    public Document? ReportDocument { get; set; }
    
    // ISO 15189 §8.8: Structured audit checklist (stored as JSON)
    public string? ChecklistJson { get; set; }

    public List<AuditFinding> Findings { get; set; } = new();

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class AuditFinding
{
    public Guid Id { get; set; }
    public Guid AuditPlanId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? IsoRequirement { get; set; }
    public FindingType Type { get; set; }
    public string? Responsible { get; set; }
    public DateTime? DueDate { get; set; }
    public string? EffectivenessReview { get; set; }
    public Guid? EvidenceDocumentId { get; set; }
    public Document? EvidenceDocument { get; set; }
    public Guid? RelatedNCId { get; set; } // Link to Nonconformity if it becomes one
    public Nonconformity? RelatedNC { get; set; }
}

public class ManagementReview
{
    public Guid Id { get; set; }
    public DateTime ReviewDate { get; set; }
    public string Participants { get; set; } = string.Empty;
    public string Agenda { get; set; } = string.Empty;
    public string? PreviousActionsReview { get; set; }
    public string? ChangesAffectingQms { get; set; }
    public string? ResourceNeeds { get; set; }
    public string? QualityIndicatorsReview { get; set; }
    public string? ExternalProviderPerformance { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Decisions { get; set; }
    public string? ImprovementOpportunities { get; set; }
    public string? Actions { get; set; }
    public string? ActionOwner { get; set; }
    public DateTime? ActionDueDate { get; set; }
    public string? EffectivenessReview { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalNotes { get; set; }
    
    public Guid? MinutesDocumentId { get; set; }
    public Document? MinutesDocument { get; set; }
}

public class QualityIndicator
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public string TargetRule { get; set; } = "Maximo";
    public decimal ActualValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Trend { get; set; } = "Estable";
    public bool MeetsTarget { get; set; }
    public string? Analysis { get; set; }
    public string? ActionPlan { get; set; }
    public string? Responsible { get; set; }
    public DateTime? DueDate { get; set; }
    public string? EffectivenessReview { get; set; }
    public DateTime? EffectivenessReviewDate { get; set; }
    public Guid? EvidenceDocumentId { get; set; }
    public Document? EvidenceDocument { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// === IQC / Control Interno (ISO 15189 §7.3.7.2) ===

public enum IQCStatus { OK, WARNING, REJECTED }

public class IQCResult
{
    public Guid Id { get; set; }
    public string EquipmentName { get; set; } = string.Empty;
    public string AnalyteName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty; // e.g. "Normal", "Patológico"
    public double Value { get; set; }
    public double Mean { get; set; }
    public double SD { get; set; }
    public DateTime Date { get; set; }
    public IQCStatus Status { get; set; } = IQCStatus.OK;
    public string? WestgardRule { get; set; } // e.g. "1-2s", "1-3s", "R-4s"
    public string? Comments { get; set; }
}

// === Planes de Contingencia (ISO 15189 §7.8) ===

public enum ContingencyStatus { DRAFT, ACTIVE, OBSOLETE }

public class ContingencyPlan
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty;
    public string ProcedureSteps { get; set; } = string.Empty;
    public string? ResponsiblePerson { get; set; }
    public DateTime? LastReviewDate { get; set; }
    public ContingencyStatus Status { get; set; } = ContingencyStatus.DRAFT;
}
