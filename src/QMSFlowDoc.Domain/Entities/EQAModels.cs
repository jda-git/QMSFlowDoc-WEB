using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Domain.Entities;

public enum EQAStatus
{
    ACTIVE,
    SUSPENDED,
    NOT_ENROLLED,
    HISTORICAL
}

public enum EQASubArea
{
    LYMPHOCYTE_POPULATIONS,
    CD34,
    LEUKEMIA_LYMPHOMA,
    LCR,
    HPN,
    EMR_MM,
    EMR_LLA,
    OTHER
}

public enum EQAResultType
{
    NUMERIC,
    PERCENTAGE,
    ABSOLUTE_COUNT,
    POSITIVE_NEGATIVE,
    DIAGNOSTIC_SUGGESTION,
    PHENOTYPE,
    INTERPRETATIVE,
    MIXED
}

public enum EQAEnrollmentStatus
{
    PENDING,
    REQUESTED,
    CONFIRMED,
    REJECTED,
    CANCELLED,
    NOT_ENROLLED
}

public enum EQARoundStatus
{
    PLANNED,
    PENDING_RECEIPT,
    RECEIVED,
    IN_PROGRESS,
    RESULTS_SUBMITTED,
    REPORT_RECEIVED,
    EVALUATED,
    CLOSED,
    CLOSED_WITH_ACTIONS,
    INCIDENCE,
    CANCELLED
}

public enum EQASampleStatus
{
    CORRECT,
    INCIDENCE,
    NOT_RECEIVED,
    PENDING_DOWNLOAD
}

public enum EQAIntegrity
{
    CORRECT,
    DOUBTFUL,
    NON_CONFORMING,
    NOT_APPLICABLE
}

public enum EQAPerformance
{
    SATISFACTORY,
    ACCEPTABLE,
    ACCEPTABLE_WITH_OBSERVATIONS,
    UNSATISFACTORY,
    NOT_EVALUABLE,
    PENDING_REVIEW
}

public enum EQADeviationSeverity
{
    LOW,
    MODERATE,
    HIGH,
    CRITICAL
}

public enum EQASampleType
{
    Physical,
    Virtual,
    FCS,
    ClinicalData,
    Images,
    Mixed
}

public class EQAProgram
{
    public Guid Id { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string CoordinatorEntity { get; set; } = string.Empty;
    public string Area { get; set; } = "Citometría de Flujo";
    public EQASubArea SubArea { get; set; } = EQASubArea.OTHER;
    public EQASampleType SampleType { get; set; } = EQASampleType.Physical;
    public string Periodicity { get; set; } = "Cuatrimestral";
    public int ExpectedRoundsPerYear { get; set; } = 3;
    public int ExpectedSamplesPerRound { get; set; } = 2;
    public string CoveredTests { get; set; } = string.Empty;
    public string EvaluatedParameters { get; set; } = string.Empty;
    public string TargetResult { get; set; } = string.Empty;
    public string GeneralAcceptanceCriteria { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }
    public EQAStatus Status { get; set; } = EQAStatus.ACTIVE;
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public List<EQAEnrollment> Enrollments { get; set; } = new();
    public List<EQAMapping> TestMappings { get; set; } = new();
    public List<EQARound> Rounds { get; set; } = new();
}

public class EQAEnrollment
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public int Year { get; set; }
    public EQAEnrollmentStatus Status { get; set; } = EQAEnrollmentStatus.PENDING;
    public DateTime? RequestDate { get; set; }
    public DateTime? ConfirmationDate { get; set; }
    public string? ParticipantCode { get; set; }
    public string? ExternalPlatformUrl { get; set; }
    public string? ExternalUser { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public decimal Cost { get; set; }
    public bool IsCritical { get; set; }
    public string? EvidenceFileName { get; set; }
    public string? EvidenceFilePath { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
}

public class EQAMapping
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid? MethodId { get; set; } // External key to Method
    public string InternalTestName { get; set; } = string.Empty;
    public string? Panel { get; set; }
    public EQAResultType ResultType { get; set; } = EQAResultType.MIXED;
    public string CoverageLevel { get; set; } = "Completa";
    public string Criticidad { get; set; } = "Media";
    public bool AlcanceAcreditado { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
}

public class EQARound
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public int Year { get; set; }
    public int RoundNumber { get; set; }
    public string ExternalCode { get; set; } = string.Empty;
    public DateTime? ExpectedReceiptDate { get; set; }
    public DateTime? RealReceiptDate { get; set; }
    public DateTime? SubmissionDeadline { get; set; }
    public DateTime? RealSubmissionDate { get; set; }
    public DateTime? ExpectedReportDate { get; set; }
    public DateTime? RealReportDate { get; set; }
    public EQARoundStatus Status { get; set; } = EQARoundStatus.PLANNED;
    public EQASampleType RoundType { get; set; } = EQASampleType.Physical;
    public string? Notes { get; set; }

    // Provider report
    public string? ReportFileName { get; set; }
    public string? ReportFilePath { get; set; }
    public string? CertificateFileName { get; set; }
    public string? CertificateFilePath { get; set; }
    public EQAPerformance GlobalOutcome { get; set; } = EQAPerformance.PENDING_REVIEW;
    public decimal? GlobalScore { get; set; }
    public string? AssessorComments { get; set; }
    public string? InternalNotes { get; set; }
    public bool RequiresAction { get; set; }
    public DateTime? InternalEvaluationDate { get; set; }
    public Guid? EvaluatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
    
    public List<EQASample> Samples { get; set; } = new();
    public List<EQADeviation> Deviations { get; set; } = new();
}

public class EQASample
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public string InternalCode { get; set; } = string.Empty;
    public string ExternalCode { get; set; } = string.Empty;
    public string SampleType { get; set; } = "Sangre Periférica";
    
    // Receipt Details
    public DateTime? ReceiptDate { get; set; }
    public EQASampleStatus ReceiptStatus { get; set; } = EQASampleStatus.CORRECT;
    public EQAIntegrity Integrity { get; set; } = EQAIntegrity.CORRECT;
    public decimal? TempAtReceipt { get; set; }
    public string? TransportConditions { get; set; }
    public decimal? ReceivedVolume { get; set; }
    public string? Anticoagulant { get; set; }
    
    // FCS / Virtual download info
    public DateTime? DownloadDate { get; set; }
    public string? FileName { get; set; }
    public string? FileFormat { get; set; }
    public long? FileSize { get; set; }
    public string? IntegrityChecksum { get; set; }
    public string? AnalysisSoftware { get; set; }
    public string? SoftwareVersion { get; set; }

    // Processing details
    public DateTime? ProcessingStart { get; set; }
    public DateTime? ProcessingEnd { get; set; }
    public string? ProcessDescription { get; set; }
    public Guid? EquipmentId { get; set; } // External key to Equipment
    public bool FollowedRoutineProcedure { get; set; } = true;
    public string? DeviationsJustification { get; set; }
    public string? ProcessingEvidencePath { get; set; }
    
    // Submitted results
    public string? SubmittedResultsJson { get; set; } // Stores key-value results as JSON
    public string? SubmittedEvidencePath { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public DateTime? SubmissionDate { get; set; }

    public string? Notes { get; set; }
}

public class EQADeviation
{
    public Guid Id { get; set; }
    public Guid RoundId { get; set; }
    public string DeviationType { get; set; } = string.Empty; // Cuantitativa, Cualitativa, etc.
    public EQADeviationSeverity Severity { get; set; } = EQADeviationSeverity.MODERATE;
    public string ProbableCause { get; set; } = string.Empty;
    public string ClinicalImpact { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = string.Empty;
    public Guid? LinkedCapaId { get; set; } // External key to CAPA / NC
    public string Status { get; set; } = "Abierta"; // Abierta, En curso, Eficaz, No eficaz, Cerrada
    public string? EvidenceFileName { get; set; }
    public string? EvidenceFilePath { get; set; }
    
    // Effectiveness
    public string? EffectivenessMethod { get; set; }
    public string? EffectivenessOutcome { get; set; } // Eficaz, Parcial, No eficaz
    public DateTime? EffectivenessEvaluationDate { get; set; }
    public string? EffectivenessEvidencePath { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
}
