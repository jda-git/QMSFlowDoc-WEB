using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Application.Services.Staff;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;
using QMSFlowDoc.Shared.DTOs;

namespace QMSFlowDoc.Infrastructure.Services.Staff;

public class StaffService : IStaffService
{
    private readonly QmsDbContext _context;

    public StaffService(QmsDbContext context)
    {
        _context = context;
    }

    public async Task<List<StaffListDto>> GetStaffListAsync(bool includeInactive = false)
    {
        var query = _context.StaffProfiles
            .Include(s => s.User)
            .AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        var list = await query.ToListAsync();
        var staffIds = list.Select(s => s.Id).ToList();

        var authCounts = await _context.StaffAuthorizations
            .Where(a => staffIds.Contains(a.StaffId) && a.Status == "VIGENTE")
            .GroupBy(a => a.StaffId)
            .Select(g => new { StaffId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StaffId, x => x.Count);

        var trainingCounts = await _context.StaffTrainings
            .Where(t => staffIds.Contains(t.StaffId) && t.Status == "ACTIVO" && t.Result == "APTO")
            .GroupBy(t => t.StaffId)
            .Select(g => new { StaffId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StaffId, x => x.Count);

        return list.Select(s => new StaffListDto(
            s.Id,
            s.UserId,
            s.User?.FullName ?? "Empleado",
            s.PositionTitle,
            s.Department,
            s.IsActive,
            trainingCounts.TryGetValue(s.Id, out var trCount) ? trCount : 0,
            authCounts.TryGetValue(s.Id, out var authCount) ? authCount : 0
        )).ToList();
    }

    public async Task<StaffExpedienteDto?> GetStaffExpedienteAsync(Guid staffId)
    {
        var staff = await _context.StaffProfiles
            .Include(s => s.User)
            .Include(s => s.Trainings).ThenInclude(t => t.TrainingActivity).ThenInclude(a => a!.TrainingType)
            .Include(s => s.CompetencyEvaluations).ThenInclude(ev => ev.Competency)
            .Include(s => s.CompetencyStatuses).ThenInclude(cs => cs.Competency)
            .Include(s => s.Authorizations).ThenInclude(a => a.Authorization)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == staffId);

        if (staff == null) return null;

        var userIds = staff.Authorizations.Select(a => a.GrantedByUserId)
            .Concat(staff.CompetencyEvaluations.Select(e => e.EvaluatorStaffId))
            .Distinct()
            .ToList();

        var evaluatorNames = await _context.StaffProfiles
            .Include(p => p.User)
            .Where(p => userIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.User?.FullName ?? "Evaluador");

        var systemUserNames = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var trainings = staff.Trainings
            .Where(t => t.Status == "ACTIVO")
            .Select(t => new StaffTrainingDto(
                t.Id,
                t.TrainingActivityId,
                t.TrainingActivity?.Title ?? "Actividad",
                t.TrainingActivity?.Provider ?? "",
                t.CompletionDate ?? DateTime.Today,
                t.TrainingActivity?.Hours ?? 0,
                t.Result
            )).ToList();

        var evaluations = staff.CompetencyEvaluations
            .Where(ev => ev.Status == "ACTIVO")
            .Select(ev => new CompetencyEvaluationDto
            {
                Id = ev.Id,
                StaffId = ev.StaffId,
                CompetencyId = ev.CompetencyId,
                CompetencyName = ev.Competency?.Name ?? "",
                Area = ev.Competency?.Area ?? "",
                EvaluationDate = ev.EvaluationDate,
                ValidUntil = ev.ValidUntil,
                Outcome = ev.Outcome,
                Evidence = ev.Findings,
                EvaluatorName = (ev.EvaluatorStaffId.HasValue && evaluatorNames.TryGetValue(ev.EvaluatorStaffId.Value, out var evalName)) ? evalName : "Evaluador",
                EvaluatorStaffId = ev.EvaluatorStaffId
            }).ToList();

        var statuses = staff.CompetencyStatuses
            .Select(cs => new StaffCompetencyStatusDto(
                cs.StaffId,
                cs.CompetencyId,
                cs.Competency?.Code ?? "",
                cs.Competency?.Name ?? "",
                cs.CurrentStatus,
                cs.LastEvaluationDate,
                cs.NextDueDate
            )).ToList();

        var authorizations = staff.Authorizations
            .Select(a => new StaffAuthorizationDto(
                a.Id,
                a.AuthorizationId,
                a.Authorization?.Name ?? "",
                a.Authorization?.Description ?? "",
                a.ValidFrom,
                a.ValidUntil,
                a.GrantedAt,
                a.Status,
                systemUserNames.TryGetValue(a.GrantedByUserId ?? Guid.Empty, out var granterName) ? granterName : "Responsable",
                a.GrantedByUserId,
                a.AssessmentMethod,
                null,
                a.EvidenceDocId,
                a.RevocationReason
            )).ToList();

        return new StaffExpedienteDto(
            staff.Id,
            staff.UserId,
            staff.User?.FullName ?? "Empleado",
            staff.PositionTitle,
            staff.Department,
            staff.HiredAt,
            staff.IsActive,
            trainings,
            evaluations,
            statuses,
            authorizations
        );
    }

    public async Task<Guid> CreateStaffProfileAsync(CreateStaffProfileRequest request)
    {
        var profile = new StaffProfile
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            PositionTitle = request.PositionTitle,
            Department = request.Department,
            HiredAt = request.HiredAt,
            IsActive = true
        };

        _context.StaffProfiles.Add(profile);
        await _context.SaveChangesAsync();
        return profile.Id;
    }

    public async Task UpdateStaffProfileAsync(Guid id, CreateStaffProfileRequest request)
    {
        var profile = await _context.StaffProfiles.FindAsync(id);
        if (profile == null) return;

        profile.UserId = request.UserId;
        profile.PositionTitle = request.PositionTitle;
        profile.Department = request.Department;
        profile.HiredAt = request.HiredAt;

        await _context.SaveChangesAsync();
    }

    public async Task<List<TrainingTypeCatalogDto>> GetTrainingTypeCatalogAsync()
    {
        return await _context.TrainingTypeCatalogs
            .Where(t => t.IsActive)
            .Select(t => new TrainingTypeCatalogDto(t.Id, t.Code, t.Name, t.IsActive))
            .ToListAsync();
    }

    public async Task<List<TrainingActivityExtendedDto>> GetTrainingActivitiesAsync()
    {
        return await (from a in _context.TrainingActivities
                      join u in _context.Users on a.CreatedByUserId equals u.Id into uj
                      from u in uj.DefaultIfEmpty()
                      select new TrainingActivityExtendedDto(
                          a.Id,
                          a.Title,
                          a.Provider,
                          a.TrainingTypeId ?? Guid.Empty,
                          a.TrainingType!.Name,
                          a.Modality,
                          a.StartDate,
                          a.EndDate,
                          a.Hours,
                          a.Credits,
                          a.Description,
                          a.IsInternal,
                          a.InternalDepartment,
                          a.Status,
                          a.Assignments.Count(asg => asg.Status == "ACTIVO"),
                          a.CreatedByUserId,
                          u != null ? u.FullName : "Admin"
                      )).ToListAsync();
    }

    public async Task<Guid> CreateTrainingActivityAsync(CreateTrainingActivityRequest request)
    {
        var act = new TrainingActivity
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Provider = request.Provider,
            TrainingTypeId = request.TrainingTypeId,
            Modality = request.Modality ?? "PRESENCIAL",
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Hours = request.Hours,
            Credits = null,
            Description = request.Description,
            IsInternal = request.IsInternal,
            InternalDepartment = null,
            Status = "ACTIVO",
            CreatedByUserId = request.CreatedByUserId
        };

        _context.TrainingActivities.Add(act);
        await _context.SaveChangesAsync();
        return act.Id;
    }

    public async Task AssignTrainingAsync(AssignStaffTrainingRequest request)
    {
        var asg = new StaffTraining
        {
            Id = Guid.NewGuid(),
            StaffId = request.StaffId,
            TrainingActivityId = request.TrainingActivityId,
            ParticipationRole = request.ParticipationRole,
            Result = request.Result,
            Score = request.Score,
            CompletionDate = request.CompletionDate,
            CertificateDocId = request.CertificateDocId,
            Notes = request.Notes,
            Status = "ACTIVO"
        };

        _context.StaffTrainings.Add(asg);
        await _context.SaveChangesAsync();
    }

    public async Task<List<CompetencyCatalogDto>> GetCompetencyCatalogAsync()
    {
        return await (from c in _context.CompetencyCatalogs
                      join u in _context.Users on c.CreatedByUserId equals u.Id into uj
                      from u in uj.DefaultIfEmpty()
                      where c.IsActive
                      select new CompetencyCatalogDto(
                          c.Id,
                          c.Code,
                          c.Name,
                          c.Description,
                          c.RoleScope,
                          c.Area,
                          c.SubArea,
                          c.DefaultReassessmentMonths,
                          c.IsActive,
                          c.CreatedByUserId,
                          u != null ? u.FullName : "Admin"
                      )).ToListAsync();
    }

    private async Task ValidateQualifiedEvaluatorAsync(Guid? evaluatorUserId)
    {
        if (!evaluatorUserId.HasValue || evaluatorUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Debe especificarse un evaluador para registrar la evaluación de competencia.");
        }

        var actualUserId = evaluatorUserId.Value;
        var staffProfile = await _context.StaffProfiles.FindAsync(evaluatorUserId.Value);
        if (staffProfile != null && staffProfile.UserId.HasValue && staffProfile.UserId.Value != Guid.Empty)
        {
            actualUserId = staffProfile.UserId.Value;
        }

        var isAuthorized = await (from ur in _context.UserRoles
                                  join r in _context.Roles on ur.RoleId equals r.Id
                                  where ur.UserId == actualUserId && 
                                        (r.Name == "Administrador" || r.Name == "Facultativo" || r.Name == "Responsable calidad")
                                  select r).AnyAsync();
        if (!isAuthorized)
        {
            throw new InvalidOperationException("El evaluador no tiene un rol cualificado (Administrador, Facultativo o Responsable calidad) para registrar evaluaciones de competencia (ISO 15189 §6.2).");
        }
    }

    public async Task RecordCompetencyEvaluationAsync(AssessCompetencyRequest request)
    {
        await ValidateQualifiedEvaluatorAsync(request.AssessedByUserId);

        // Encontrar una competencia en base al nombre o Id si estuviera mapeada
        var competency = await _context.CompetencyCatalogs.FirstOrDefaultAsync(c => c.Name == request.CompetencyName);
        if (competency == null) return;

        var validMonths = competency.DefaultReassessmentMonths;
        var validUntil = request.EvaluationDate.AddMonths(validMonths);

        var eval = new CompetencyEvaluation
        {
            Id = Guid.NewGuid(),
            StaffId = request.StaffId,
            CompetencyId = competency.Id,
            EvaluationDate = request.EvaluationDate,
            EvaluatorStaffId = request.AssessedByUserId,
            Outcome = request.Outcome.ToString(),
            ValidUntil = validUntil,
            NextDueDate = validUntil,
            Findings = request.Evidence,
            Status = "ACTIVO"
        };

        _context.CompetencyEvaluations.Add(eval);

        // Update StaffCompetencyStatus
        var status = await _context.StaffCompetencyStatuses
            .FirstOrDefaultAsync(s => s.StaffId == request.StaffId && s.CompetencyId == competency.Id);

        if (status == null)
        {
            status = new StaffCompetencyStatus
            {
                StaffId = request.StaffId,
                CompetencyId = competency.Id
            };
            _context.StaffCompetencyStatuses.Add(status);
        }

        status.CurrentStatus = request.Outcome == QMSFlowDoc.Shared.Models.CompetencyOutcome.PASS ? "APTO" : "NO_APTO";
        status.LastEvaluationId = eval.Id;
        status.LastEvaluationDate = request.EvaluationDate;
        status.NextDueDate = validUntil;
        status.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<List<AuthorizationCatalogDto>> GetAuthorizationCatalogAsync()
    {
        return await (from a in _context.AuthorizationCatalogs
                      join u in _context.Users on a.CreatedByUserId equals u.Id into uj
                      from u in uj.DefaultIfEmpty()
                      where a.IsActive
                      select new AuthorizationCatalogDto(
                          a.Id,
                          a.Code,
                          a.Name,
                          a.Description,
                          a.RoleScope,
                          a.RequiresCompetency,
                          a.ValidityMonths,
                          a.IsActive,
                          a.RequiredCompetencies.Select(rc => (Guid?)rc.CompetencyId).FirstOrDefault(),
                          a.RequiredCompetencies.Select(rc => rc.Competency != null ? rc.Competency.Name : null).FirstOrDefault(),
                          a.CreatedByUserId,
                          u != null ? u.FullName : "Admin"
                      )).ToListAsync();
    }

    private async Task ValidateAuthorizationRequestAsync(GrantAuthorizationRequest request)
    {
        if (!request.EvidenceDocId.HasValue || request.EvidenceDocId == Guid.Empty)
        {
            throw new InvalidOperationException("Se requiere un documento de evidencia obligatoria (EvidenceDocId) para conceder o actualizar la autorización (ISO 15189 §6.2).");
        }

        if (string.IsNullOrWhiteSpace(request.AssessmentMethod))
        {
            throw new InvalidOperationException("Se requiere una justificación textual en el método de evaluación (AssessmentMethod) para la autorización (ISO 15189 §6.2).");
        }

        var authCatalog = await _context.AuthorizationCatalogs
            .Include(a => a.RequiredCompetencies)
            .FirstOrDefaultAsync(a => a.Name == request.TaskName);
        if (authCatalog == null) return;

        if (authCatalog.RequiresCompetency)
        {
            var requiredCompetencyIds = authCatalog.RequiredCompetencies.Select(rc => rc.CompetencyId).ToList();
            if (!requiredCompetencyIds.Any())
            {
                requiredCompetencyIds = await _context.AuthorizationRequiredCompetencies
                    .Where(rc => rc.AuthorizationId == authCatalog.Id)
                    .Select(rc => rc.CompetencyId)
                    .ToListAsync();
            }

            foreach (var compId in requiredCompetencyIds)
            {
                var compStatus = await _context.StaffCompetencyStatuses
                    .FirstOrDefaultAsync(cs => cs.StaffId == request.StaffId && cs.CompetencyId == compId);

                if (compStatus == null || compStatus.CurrentStatus != "APTO" || (compStatus.NextDueDate.HasValue && compStatus.NextDueDate.Value <= DateTime.UtcNow))
                {
                    var compName = (await _context.CompetencyCatalogs.FindAsync(compId))?.Name ?? "Competencia requerida";
                    throw new InvalidOperationException($"El empleado no cuenta con la competencia '{compName}' en estado APTO y vigente.");
                }
            }
        }
    }

    private async Task LogAuditAsync(string action, string entityType, Guid? entityId, string details, Guid? userId, string? username)
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

    public async Task GrantAuthorizationAsync(GrantAuthorizationRequest request)
    {
        await ValidateAuthorizationRequestAsync(request);

        var authCatalog = await _context.AuthorizationCatalogs.FirstOrDefaultAsync(a => a.Name == request.TaskName);
        if (authCatalog == null) return;

        var auth = new StaffAuthorization
        {
            Id = Guid.NewGuid(),
            StaffId = request.StaffId,
            AuthorizationId = authCatalog.Id,
            GrantedByUserId = request.GrantedByUserId,
            GrantedAt = DateTime.UtcNow,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            Status = "VIGENTE",
            AssessmentMethod = request.AssessmentMethod,
            EvidenceDocId = request.EvidenceDocId
        };

        _context.StaffAuthorizations.Add(auth);
        await _context.SaveChangesAsync();
        await LogAuditAsync("CREATE", "StaffAuthorization", auth.Id, $"Autorización concedida para {request.TaskName}", request.GrantedByUserId, null);
        await _context.SaveChangesAsync();
    }

    public async Task<List<UserLookupDto>> GetAvailableUsersLookupAsync()
    {
        return await _context.Users
            .Where(u => u.IsActive)
            .Select(u => new UserLookupDto(u.Id, u.FullName, u.Email ?? ""))
            .ToListAsync();
    }

    public async Task<Guid> CreateCompetencyCatalogAsync(CreateCompetencyCatalogRequest request)
    {
        var comp = new CompetencyCatalog
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            RoleScope = request.RoleScope,
            Area = request.Area,
            SubArea = request.SubArea,
            DefaultReassessmentMonths = request.DefaultReassessmentMonths,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = request.CreatedByUserId
        };

        _context.CompetencyCatalogs.Add(comp);
        await _context.SaveChangesAsync();
        return comp.Id;
    }

    public async Task UpdateCompetencyCatalogAsync(Guid id, CreateCompetencyCatalogRequest request)
    {
        var comp = await _context.CompetencyCatalogs.FirstOrDefaultAsync(c => c.Id == id);
        if (comp == null) return;

        comp.Code = request.Code;
        comp.Name = request.Name;
        comp.Description = request.Description;
        comp.RoleScope = request.RoleScope;
        comp.Area = request.Area;
        comp.SubArea = request.SubArea;
        comp.DefaultReassessmentMonths = request.DefaultReassessmentMonths;
        if (request.CreatedByUserId.HasValue && request.CreatedByUserId.Value != Guid.Empty)
        {
            comp.CreatedByUserId = request.CreatedByUserId.Value;
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteCompetencyCatalogAsync(Guid id)
    {
        var comp = await _context.CompetencyCatalogs.FirstOrDefaultAsync(c => c.Id == id);
        if (comp == null) return;

        comp.IsActive = false;
        await _context.SaveChangesAsync();
    }

    public async Task<Guid> CreateAuthorizationCatalogAsync(CreateAuthorizationCatalogRequest request)
    {
        var auth = new AuthorizationCatalog
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            RoleScope = request.RoleScope,
            RequiresCompetency = request.RequiresCompetency,
            ValidityMonths = request.ValidityMonths,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = request.CreatedByUserId
        };

        _context.AuthorizationCatalogs.Add(auth);

        if (request.RequiresCompetency && request.RequiredCompetencyId.HasValue && request.RequiredCompetencyId.Value != Guid.Empty)
        {
            _context.AuthorizationRequiredCompetencies.Add(new AuthorizationRequiredCompetency
            {
                AuthorizationId = auth.Id,
                CompetencyId = request.RequiredCompetencyId.Value
            });
        }

        await _context.SaveChangesAsync();
        return auth.Id;
    }

    public async Task UpdateAuthorizationCatalogAsync(Guid id, CreateAuthorizationCatalogRequest request)
    {
        var auth = await _context.AuthorizationCatalogs
            .Include(a => a.RequiredCompetencies)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (auth == null) return;

        auth.Code = request.Code;
        auth.Name = request.Name;
        auth.Description = request.Description;
        auth.RoleScope = request.RoleScope;
        auth.RequiresCompetency = request.RequiresCompetency;
        auth.ValidityMonths = request.ValidityMonths;
        if (request.CreatedByUserId.HasValue && request.CreatedByUserId.Value != Guid.Empty)
        {
            auth.CreatedByUserId = request.CreatedByUserId.Value;
        }

        // Clear existing competencies
        _context.AuthorizationRequiredCompetencies.RemoveRange(auth.RequiredCompetencies);
        auth.RequiredCompetencies.Clear();

        if (request.RequiresCompetency && request.RequiredCompetencyId.HasValue && request.RequiredCompetencyId.Value != Guid.Empty)
        {
            _context.AuthorizationRequiredCompetencies.Add(new AuthorizationRequiredCompetency
            {
                AuthorizationId = auth.Id,
                CompetencyId = request.RequiredCompetencyId.Value
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAuthorizationCatalogAsync(Guid id)
    {
        var auth = await _context.AuthorizationCatalogs.FirstOrDefaultAsync(a => a.Id == id);
        if (auth == null) return;

        auth.IsActive = false;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateCompetencyEvaluationAsync(Guid id, AssessCompetencyRequest request)
    {
        await ValidateQualifiedEvaluatorAsync(request.AssessedByUserId);

        var eval = await _context.CompetencyEvaluations.FirstOrDefaultAsync(e => e.Id == id);
        if (eval == null) return;

        var competency = await _context.CompetencyCatalogs.FirstOrDefaultAsync(c => c.Name == request.CompetencyName);
        if (competency == null) return;

        var validMonths = competency.DefaultReassessmentMonths;
        var validUntil = request.EvaluationDate.AddMonths(validMonths);

        eval.EvaluationDate = request.EvaluationDate;
        eval.EvaluatorStaffId = request.AssessedByUserId;
        eval.Outcome = request.Outcome.ToString();
        eval.ValidUntil = validUntil;
        eval.NextDueDate = validUntil;
        eval.Findings = request.Evidence;

        // Update StaffCompetencyStatus if this is the latest one
        var status = await _context.StaffCompetencyStatuses
            .FirstOrDefaultAsync(s => s.StaffId == request.StaffId && s.CompetencyId == competency.Id);

        if (status != null && status.LastEvaluationId == eval.Id)
        {
            status.CurrentStatus = request.Outcome == QMSFlowDoc.Shared.Models.CompetencyOutcome.PASS ? "APTO" : "NO_APTO";
            status.LastEvaluationDate = request.EvaluationDate;
            status.NextDueDate = validUntil;
            status.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteCompetencyEvaluationAsync(Guid id)
    {
        var eval = await _context.CompetencyEvaluations.FirstOrDefaultAsync(e => e.Id == id);
        if (eval == null) return;

        // Find another evaluation for the same staff and competency
        var otherEvals = await _context.CompetencyEvaluations
            .Where(e => e.StaffId == eval.StaffId && e.CompetencyId == eval.CompetencyId && e.Id != id && e.Status == "ACTIVO")
            .OrderByDescending(e => e.EvaluationDate)
            .FirstOrDefaultAsync();

        var status = await _context.StaffCompetencyStatuses
            .FirstOrDefaultAsync(s => s.StaffId == eval.StaffId && s.CompetencyId == eval.CompetencyId);

        if (status != null)
        {
            if (otherEvals != null)
            {
                status.CurrentStatus = otherEvals.Outcome == "PASS" || otherEvals.Outcome == "APTO" ? "APTO" : "NO_APTO";
                status.LastEvaluationId = otherEvals.Id;
                status.LastEvaluationDate = otherEvals.EvaluationDate;
                status.NextDueDate = otherEvals.ValidUntil;
                status.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.StaffCompetencyStatuses.Remove(status);
            }
        }

        _context.CompetencyEvaluations.Remove(eval);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateStaffAuthorizationAsync(Guid id, GrantAuthorizationRequest request)
    {
        await ValidateAuthorizationRequestAsync(request);

        var auth = await _context.StaffAuthorizations.FirstOrDefaultAsync(a => a.Id == id);
        if (auth == null) return;

        var authCatalog = await _context.AuthorizationCatalogs.FirstOrDefaultAsync(a => a.Name == request.TaskName);
        if (authCatalog == null) return;

        auth.AuthorizationId = authCatalog.Id;
        auth.GrantedByUserId = request.GrantedByUserId;
        auth.ValidFrom = request.ValidFrom;
        auth.ValidUntil = request.ValidUntil;
        auth.AssessmentMethod = request.AssessmentMethod;
        auth.EvidenceDocId = request.EvidenceDocId;
        auth.UpdatedAt = DateTime.UtcNow;

        await LogAuditAsync("EDIT", "StaffAuthorization", auth.Id, $"Autorización editada para {request.TaskName}", request.GrantedByUserId, null);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteStaffAuthorizationAsync(Guid id, string reason, Guid? userId = null, string? userName = null)
    {
        var auth = await _context.StaffAuthorizations.FirstOrDefaultAsync(a => a.Id == id);
        if (auth == null) return;

        auth.Status = "REVOCADA";
        auth.RevocationReason = reason;
        auth.UpdatedAt = DateTime.UtcNow;

        await LogAuditAsync("REVOKE", "StaffAuthorization", auth.Id, $"Autorización revocada: {reason}", userId, userName);
        await _context.SaveChangesAsync();
    }
}
