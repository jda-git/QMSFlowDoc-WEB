using System;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Shared.DTOs;

public class StaffListDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; } // Added for user linkage
    public string FullName { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; }
    public int TrainingCount { get; set; }
    public int CompetencyPassCount { get; set; }

    public string? RoleName { get; set; }

    public StaffListDto() { }
    public StaffListDto(Guid id, Guid? userId, string name, string? pos, string? dept, bool active, int training, int competency, string? roleName = null)
    {
        Id = id; UserId = userId; FullName = name; PositionTitle = pos; Department = dept;
        IsActive = active; TrainingCount = training; CompetencyPassCount = competency;
        RoleName = roleName;
    }
}

public record CreateStaffProfileRequest(
    Guid UserId,
    string? PositionTitle,
    string? Department,
    DateTime? HiredAt
);

public record UpdateStaffProfileRequest(
    Guid Id,
    string FullName,
    string? Email,
    string? PositionTitle,
    string? Department,
    DateTime? HiredAt,
    string RoleName,
    bool IsActive
);

public record StaffProfileDetailDto(
    Guid Id,
    Guid UserId,
    string Username,
    string FullName,
    string? Email,
    string? PositionTitle,
    string? Department,
    DateTime? HiredAt,
    string RoleName,
    bool IsActive
);

// Registro de formación - ahora con texto libre
public record RegisterTrainingRequest(
    Guid StaffId,
    string Title,
    string? Provider,
    decimal Hours,
    DateTime CompletedAt,
    string Result,
    string? Notes,
    Guid? CompetencyId = null,
    string? SourceCertificatePath = null
);

public record UpdateTrainingRequest(
    Guid Id,
    Guid StaffId,
    string Title,
    string? Provider,
    decimal Hours,
    DateTime CompletedAt,
    string Result,
    string? Notes,
    Guid? CompetencyId = null,
    string? SourceCertificatePath = null
);

// Evaluación de competencia - ahora con texto libre
public record AssessCompetencyRequest(
    Guid StaffId,
    string CompetencyName,
    string Area,
    CompetencyOutcome Outcome,
    DateTime EvaluationDate,
    DateTime? ValidUntil,
    string? Evidence,
    Guid? AssessedByUserId = null
);

// Emisión de autorización - ahora con texto libre
public record GrantAuthorizationRequest(
    Guid StaffId,
    string TaskName,
    string? Description,
    DateTime ValidFrom,

    DateTime? ValidUntil,
    Guid? GrantedByUserId = null,
    Guid? CompetencyId = null,
    Guid? EvaluationId = null,
    string? AssessmentMethod = null
);
