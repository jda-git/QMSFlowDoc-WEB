using QMSFlowDoc.Domain.Identity;
using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Domain.Entities;

public class AuthorizationCatalog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty; // e.g. "AUTH-CF-RESULTS"
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RoleScope { get; set; } = "AMBOS"; // TECNICO, FACULTATIVO, AMBOS
    public bool RequiresCompetency { get; set; }
    public int? ValidityMonths { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }
    
    // Relation with required competencies
    public List<AuthorizationRequiredCompetency> RequiredCompetencies { get; set; } = new();
}

public class AuthorizationRequiredCompetency
{
    public Guid AuthorizationId { get; set; }
    public AuthorizationCatalog? Authorization { get; set; }
    public Guid CompetencyId { get; set; }
    public CompetencyCatalog? Competency { get; set; }
}

public class StaffAuthorization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffId { get; set; }
    public StaffProfile? Staff { get; set; }
    
    public Guid AuthorizationId { get; set; }
    public AuthorizationCatalog? Authorization { get; set; }
    
    public Guid? GrantedByUserId { get; set; }
    public ApplicationUser? GrantedByUser { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    
    public string Status { get; set; } = "VIGENTE"; // VIGENTE, CADUCADA, REVOCADA
    public string? RevocationReason { get; set; }
    public string? AssessmentMethod { get; set; }
    
    public Guid? EvidenceDocId { get; set; } // Link to Document System
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // V2: Optimistic concurrency
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
