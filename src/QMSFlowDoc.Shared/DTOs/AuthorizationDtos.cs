using System;

namespace QMSFlowDoc.Shared.DTOs;

public record AuthorizationDto(
    Guid Id,
    string Code,
    string Name,
    string? Description
);

// Extended DTO for list display with all required columns
public record StaffAuthorizationDto(
    Guid Id,
    Guid AuthorizationId,
    string AuthorizationName,
    string? Description,
    DateTime ValidFrom,
    DateTime? ValidUntil,
    DateTime GrantedAt,
    string Status,
    string? GrantedByName,
    Guid? GrantedByUserId = null,
    string? AssessmentMethod = null,
    Guid? CompetencyId = null, // Added
    Guid? EvidenceDocId = null,
    string? RevocationReason = null
);

public class GlobalAuthorizationDto
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffPosition { get; set; } = string.Empty;
    public Guid AuthorizationId { get; set; }
    public string AuthorizationName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime GrantedAt { get; set; }
    public string Status { get; set; } = "Active";
    public Guid? GrantedBy { get; set; }
    public string? GrantedByName { get; set; }
    public Guid? CompetencyId { get; set; }
    public Guid? EvaluationId { get; set; }
}
