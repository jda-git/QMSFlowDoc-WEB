using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QMSFlowDoc.Shared.DTOs;

namespace QMSFlowDoc.Application.Services.Staff;

public interface IStaffService
{
    // Staff Profiles
    Task<List<StaffListDto>> GetStaffListAsync(bool includeInactive = false);
    Task<StaffExpedienteDto?> GetStaffExpedienteAsync(Guid staffId);
    Task<Guid> CreateStaffProfileAsync(CreateStaffProfileRequest request);
    Task UpdateStaffProfileAsync(Guid id, CreateStaffProfileRequest request);
    
    // Training Activities & Catalog
    Task<List<TrainingTypeCatalogDto>> GetTrainingTypeCatalogAsync();
    Task<List<TrainingActivityExtendedDto>> GetTrainingActivitiesAsync();
    Task<Guid> CreateTrainingActivityAsync(CreateTrainingActivityRequest request);
    Task AssignTrainingAsync(AssignStaffTrainingRequest request);
    
    // Competency Evaluation & Catalog
    Task<List<CompetencyCatalogDto>> GetCompetencyCatalogAsync();
    Task RecordCompetencyEvaluationAsync(AssessCompetencyRequest request);
    Task<Guid> CreateCompetencyCatalogAsync(CreateCompetencyCatalogRequest request);
    Task UpdateCompetencyCatalogAsync(Guid id, CreateCompetencyCatalogRequest request);
    Task DeleteCompetencyCatalogAsync(Guid id);
    
    // Authorizations
    Task<List<AuthorizationCatalogDto>> GetAuthorizationCatalogAsync();
    Task GrantAuthorizationAsync(GrantAuthorizationRequest request);
    Task UpdateStaffAuthorizationAsync(Guid id, GrantAuthorizationRequest request);
    Task DeleteStaffAuthorizationAsync(Guid id);
    Task<Guid> CreateAuthorizationCatalogAsync(CreateAuthorizationCatalogRequest request);
    Task UpdateAuthorizationCatalogAsync(Guid id, CreateAuthorizationCatalogRequest request);
    Task DeleteAuthorizationCatalogAsync(Guid id);
    
    // Competency Evaluation update/delete
    Task UpdateCompetencyEvaluationAsync(Guid id, AssessCompetencyRequest request);
    Task DeleteCompetencyEvaluationAsync(Guid id);
    
    // Helpers
    Task<List<UserLookupDto>> GetAvailableUsersLookupAsync();
}

public record CreateCompetencyCatalogRequest(
    string Code,
    string Name,
    string? Description,
    string RoleScope,
    string Area,
    string? SubArea,
    int DefaultReassessmentMonths,
    Guid? CreatedByUserId = null
);

public record CreateAuthorizationCatalogRequest(
    string Code,
    string Name,
    string? Description,
    string RoleScope,
    bool RequiresCompetency,
    int? ValidityMonths,
    Guid? RequiredCompetencyId = null,
    Guid? CreatedByUserId = null
);

public record StaffExpedienteDto(
    Guid Id,
    Guid? UserId,
    string FullName,
    string? PositionTitle,
    string? Department,
    DateTime? HiredAt,
    bool IsActive,
    List<StaffTrainingDto> Trainings,
    List<CompetencyEvaluationDto> CompetencyEvaluations,
    List<StaffCompetencyStatusDto> CompetencyStatuses,
    List<StaffAuthorizationDto> Authorizations
);

public record StaffCompetencyStatusDto(
    Guid StaffId,
    Guid CompetencyId,
    string CompetencyCode,
    string CompetencyName,
    string CurrentStatus,
    DateTime? LastEvaluationDate,
    DateTime? NextDueDate
);

public record CompetencyCatalogDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string RoleScope,
    string Area,
    string? SubArea,
    int DefaultReassessmentMonths,
    bool IsActive,
    Guid? CreatedByUserId = null,
    string? CreatedByUserName = null
);

public record AuthorizationCatalogDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string RoleScope,
    bool RequiresCompetency,
    int? ValidityMonths,
    bool IsActive,
    Guid? RequiredCompetencyId = null,
    string? RequiredCompetencyName = null,
    Guid? CreatedByUserId = null,
    string? CreatedByUserName = null
);

public record TrainingTypeCatalogDto(Guid Id, string Code, string Name, bool IsActive);
public record UserLookupDto(Guid Id, string FullName, string Email);
public record AssignStaffTrainingRequest(
    Guid StaffId,
    Guid TrainingActivityId,
    string ParticipationRole,
    string? Result,
    string? Score,
    DateTime? CompletionDate,
    Guid? CertificateDocId,
    string? Notes
);
