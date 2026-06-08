using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Application.Services.Quality;
using Microsoft.AspNetCore.Identity;
using QMSFlowDoc.Domain.Identity;
using QMSFlowDoc.Infrastructure.Persistence;
using QMSFlowDoc.Shared.DTOs;
using DomainEntities = QMSFlowDoc.Domain.Entities;
using SharedModels = QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Infrastructure.Services.Quality;

public class QualityService : IQualityService
{
    private readonly QmsDbContext _context;
    private readonly UserManager<ApplicationUser>? _userManager;

    public QualityService(QmsDbContext context, UserManager<ApplicationUser>? userManager = null)
    {
        _context = context;
        _userManager = userManager;
    }

    // ── Non-Conformities ─────────────────────────────────────────────

    public async Task<List<NCListDto>> GetNonconformitiesAsync()
    {
        return await _context.Nonconformities
            .Where(n => !n.IsDeleted)
            .Include(n => n.Actions)
            .OrderByDescending(n => n.DetectedAt)
            .Select(n => new NCListDto
            {
                Id = n.Id,
                DetectedAt = n.DetectedAt,
                Title = n.Title,
                Severity = (SharedModels.NCSeverity)n.Severity,
                Status = (SharedModels.NCStatus)n.Status,
                ImpactPatient = n.ImpactPatient,
                ActionCount = n.Actions.Count(a => !a.IsDeleted),
                DueDate = n.DueDate,
                Origin = n.Origin,
                RootCauseAnalysis = n.RootCauseAnalysis
            })
            .ToListAsync();
    }

    public async Task<SharedModels.Nonconformity?> GetNCByIdAsync(Guid id)
    {
        var nc = await _context.Nonconformities
            .Include(n => n.Actions)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);

        if (nc == null) return null;

        // Fetch user names for closure and actions
        string? closedByName = null;
        if (nc.ClosedByUserId.HasValue)
        {
            closedByName = (await _context.Users.FindAsync(nc.ClosedByUserId.Value))?.FullName;
        }

        // Fetch user names for actions
        var actionDtos = new List<SharedModels.CapaAction>();
        foreach (var a in nc.Actions.Where(x => !x.IsDeleted))
        {
            string? actionClosedByName = null;
            if (a.ClosedByUserId.HasValue)
            {
                actionClosedByName = (await _context.Users.FindAsync(a.ClosedByUserId.Value))?.FullName;
            }
            actionDtos.Add(new SharedModels.CapaAction
            {
                Id = a.Id,
                NCId = a.NCId,
                ActionType = (SharedModels.CAPAActionType)a.ActionType,
                Description = a.Description,
                OwnerUserId = a.OwnerUserId,
                ClosedByUserId = a.ClosedByUserId,
                ClosedByUserName = actionClosedByName,
                DueDate = a.DueDate,
                CompletedAt = a.CompletedAt,
                EffectivenessCheck = a.EffectivenessCheck,
                Status = (SharedModels.CAPAStatus)a.Status
            });
        }

        return new SharedModels.Nonconformity
        {
            Id = nc.Id,
            DetectedAt = nc.DetectedAt,
            DetectedByUserId = nc.DetectedByUserId,
            Title = nc.Title,
            Description = nc.Description,
            Severity = (SharedModels.NCSeverity)nc.Severity,
            ImpactPatient = nc.ImpactPatient,
            Containment = nc.Containment,
            DueDate = nc.DueDate,
            Origin = nc.Origin,
            RootCauseAnalysis = nc.RootCauseAnalysis,
            Status = (SharedModels.NCStatus)nc.Status,
            UpdatedAt = nc.UpdatedAt,
            ClosedAt = nc.ClosedAt,
            ClosedByUserId = nc.ClosedByUserId,
            ClosedByUserName = closedByName,
            RowVersion = nc.RowVersion,
            Actions = actionDtos
        };
    }

    public async Task<Guid> CreateNCAsync(CreateNCRequest request, Guid? userId = null, string? userName = null)
    {
        var nc = new DomainEntities.Nonconformity
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Severity = (DomainEntities.NCSeverity)request.Severity,
            Status = request.Status.HasValue ? (DomainEntities.NCStatus)request.Status.Value : DomainEntities.NCStatus.OPEN,
            ImpactPatient = request.ImpactPatient,
            Containment = request.Containment,
            DueDate = request.DueDate,
            Origin = request.Origin,
            RootCauseAnalysis = request.RootCauseAnalysis,
            DetectedByUserId = request.DetectedByUserId,
            DetectedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Nonconformities.Add(nc);
        await _context.SaveChangesAsync();

        await LogAuditAsync("CREATE", "Nonconformity", nc.Id, $"NC '{nc.Title}' creada", userId, userName);
        await _context.SaveChangesAsync();

        return nc.Id;
    }

    public async Task<bool> UpdateNCAsync(Guid id, CreateNCRequest request, Guid? userId = null, string? userName = null)
    {
        var nc = await _context.Nonconformities.FindAsync(id);
        if (nc == null || nc.IsDeleted) return false;

        nc.Title = request.Title;
        nc.Description = request.Description;
        nc.Severity = (DomainEntities.NCSeverity)request.Severity;
        nc.ImpactPatient = request.ImpactPatient;
        nc.Containment = request.Containment;
        nc.DueDate = request.DueDate;
        nc.Origin = request.Origin;
        nc.RootCauseAnalysis = request.RootCauseAnalysis;
        nc.DetectedByUserId = request.DetectedByUserId;
        if (request.Status.HasValue)
        {
            var targetStatus = (DomainEntities.NCStatus)request.Status.Value;
            if (targetStatus == DomainEntities.NCStatus.CLOSED)
            {
                throw new InvalidOperationException("El cierre formal de una No Conformidad debe realizarse mediante cambio de estado con firma electronica.");
            }
            nc.Status = targetStatus;
        }
        nc.UpdatedAt = DateTime.UtcNow;

        await LogAuditAsync("EDIT", "Nonconformity", nc.Id, $"NC '{nc.Title}' editada", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateNCStatusAsync(Guid id, SharedModels.NCStatus status, Guid? userId = null, string? userName = null, string? confirmPassword = null)
    {
        var nc = await _context.Nonconformities.FindAsync(id);
        if (nc == null || nc.IsDeleted) return false;

        var targetStatus = (DomainEntities.NCStatus)status;
        if (targetStatus == DomainEntities.NCStatus.CLOSED)
        {
            await _context.Entry(nc).Collection(n => n.Actions).LoadAsync();
            ValidateNCClosure(nc);
            await ValidateNCClosureSignatureAsync(userId, confirmPassword);
            nc.ClosedAt = DateTime.UtcNow;
            nc.ClosedByUserId = userId;
        }
        else
        {
            nc.ClosedAt = null;
            nc.ClosedByUserId = null;
        }

        nc.Status = targetStatus;
        nc.UpdatedAt = DateTime.UtcNow;

        await LogAuditAsync("STATUS_CHANGE", "Nonconformity", nc.Id, $"NC estado cambiado a {status}", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    private async Task ValidateNCClosureSignatureAsync(Guid? userId, string? confirmPassword)
    {
        if (!userId.HasValue)
        {
            throw new InvalidOperationException("Usuario no identificado para firmar el cierre de la No Conformidad.");
        }

        if (string.IsNullOrWhiteSpace(confirmPassword))
        {
            throw new InvalidOperationException("Se requiere la contraseña del usuario para firmar el cierre de la No Conformidad.");
        }

        if (_userManager == null)
        {
            throw new InvalidOperationException("No se pudo validar la firma electronica del cierre de la No Conformidad.");
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null || !await _userManager.CheckPasswordAsync(user, confirmPassword))
        {
            throw new InvalidOperationException("La contraseña de firma no es valida. No se pudo cerrar la No Conformidad.");
        }
    }

    public async Task<bool> DeleteNCAsync(Guid id, Guid? userId = null, string? userName = null)
    {
        var nc = await _context.Nonconformities
            .Include(n => n.Actions)
            .FirstOrDefaultAsync(n => n.Id == id);
            
        if (nc == null || nc.IsDeleted) return false;

        // Soft delete NC
        nc.IsDeleted = true;
        nc.DeletedAt = DateTime.UtcNow;
        nc.DeletedByUserId = userId;

        // Soft delete associated CAPA actions
        foreach (var action in nc.Actions)
        {
            action.IsDeleted = true;
            action.DeletedAt = DateTime.UtcNow;
            action.DeletedByUserId = userId;
        }

        await LogAuditAsync("SOFT_DELETE", "Nonconformity", nc.Id, $"NC '{nc.Title}' eliminada lógicamente (conservación de evidencia histórica - ISO 15189 §8.7)", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── CAPA Actions ─────────────────────────────────────────────────

    public async Task<Guid> CreateCAPAAsync(CreateCAPARequest request, Guid? userId = null, string? userName = null)
    {
        var capa = new DomainEntities.CapaAction
        {
            Id = Guid.NewGuid(),
            NCId = request.NCId,
            ActionType = (DomainEntities.CAPAActionType)request.ActionType,
            Description = request.Description,
            OwnerUserId = request.OwnerUserId,
            DueDate = request.DueDate,
            Status = DomainEntities.CAPAStatus.OPEN
        };

        _context.CapaActions.Add(capa);
        await _context.SaveChangesAsync();

        await LogAuditAsync("CREATE", "CapaAction", capa.Id, $"Acción CAPA creada para NC {request.NCId}", userId, userName);
        await _context.SaveChangesAsync();

        return capa.Id;
    }

    public async Task<bool> UpdateCAPAStatusAsync(Guid id, SharedModels.CAPAStatus status, Guid? userId = null, string? userName = null)
    {
        var capa = await _context.CapaActions.FindAsync(id);
        if (capa == null || capa.IsDeleted) return false;

        capa.Status = (DomainEntities.CAPAStatus)status;
        await LogAuditAsync("STATUS_CHANGE", "CapaAction", capa.Id, $"Acción CAPA estado cambiado a {status}", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> CompleteCAPAAsync(Guid id, string? effectivenessCheck, Guid? userId = null, string? userName = null)
    {
        var capa = await _context.CapaActions.FindAsync(id);
        if (capa == null || capa.IsDeleted) return false;

        // ISO 15189 §8.7.2 - Validación fuerte de eficacia
        if (string.IsNullOrWhiteSpace(effectivenessCheck))
        {
            throw new InvalidOperationException("Debe ingresar la verificación de la eficacia para completar la acción CAPA (ISO 15189 §8.7.2).");
        }

        capa.Status = DomainEntities.CAPAStatus.DONE;
        capa.CompletedAt = DateTime.UtcNow;
        capa.EffectivenessCheck = effectivenessCheck;
        capa.ClosedByUserId = userId;

        await LogAuditAsync("COMPLETE", "CapaAction", capa.Id, "Acción CAPA completada con verificación de eficacia", userId, userName);

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> VerifyCAPAAsync(Guid id, string? verificationNotes, Guid? userId = null, string? userName = null)
    {
        var capa = await _context.CapaActions.FindAsync(id);
        if (capa == null || capa.IsDeleted) return false;

        if (userId.HasValue)
        {
            var isAuthorized = await (from ur in _context.UserRoles
                                      join r in _context.Roles on ur.RoleId equals r.Id
                                      where ur.UserId == userId.Value && (r.Name == "Administrador" || r.Name == "Responsable calidad")
                                      select r).AnyAsync();
            if (!isAuthorized)
            {
                throw new UnauthorizedAccessException("Únicamente los roles de Administrador o Responsable calidad pueden verificar la eficacia de las acciones CAPA (ISO 15189 §8.7.2).");
            }
        }

        capa.Status = DomainEntities.CAPAStatus.VERIFIED;
        capa.EffectivenessCheck = $"{capa.EffectivenessCheck} [Verificado por {userName ?? "Usuario"} el {DateTime.UtcNow:dd/MM/yyyy}: {verificationNotes}]";

        await LogAuditAsync("VERIFY", "CapaAction", capa.Id, $"Acción CAPA verificada: {verificationNotes}", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteCAPAAsync(Guid id, Guid? userId = null, string? userName = null)
    {
        var capa = await _context.CapaActions.FindAsync(id);
        if (capa == null || capa.IsDeleted) return false;

        capa.IsDeleted = true;
        capa.DeletedAt = DateTime.UtcNow;
        capa.DeletedByUserId = userId;

        await LogAuditAsync("SOFT_DELETE", "CapaAction", capa.Id, "Acción CAPA eliminada lógicamente (conservación de evidencia histórica - ISO 15189 §8.7)", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Complaints ───────────────────────────────────────────────────

    public async Task<List<ComplaintListDto>> GetComplaintsAsync()
    {
        return await _context.Complaints
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.Date)
            .Select(c => new ComplaintListDto
            {
                Id = c.Id,
                Date = c.Date,
                Source = c.Source,
                Description = c.Description,
                Category = (SharedModels.ComplaintCategory)c.Category,
                Status = (SharedModels.ComplaintStatus)c.Status
            })
            .ToListAsync();
    }

    public async Task<SharedModels.Complaint?> GetComplaintByIdAsync(Guid id)
    {
        var c = await _context.Complaints
            .Include(x => x.Actions)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (c == null) return null;

        string? closedByName = null;
        if (c.ClosedByUserId.HasValue)
        {
            closedByName = (await _context.Users.FindAsync(c.ClosedByUserId.Value))?.FullName;
        }

        return new SharedModels.Complaint
        {
            Id = c.Id,
            Date = c.Date,
            Source = c.Source,
            Description = c.Description,
            Category = (SharedModels.ComplaintCategory)c.Category,
            ClaimantType = (SharedModels.ClaimantType)c.ClaimantType,
            IsSubstantiated = c.IsSubstantiated,
            ReceiptDate = c.ReceiptDate,
            ReceiptMethod = c.ReceiptMethod,
            ClinicalImpact = (SharedModels.ClinicalImpact)c.ClinicalImpact,
            RelatedNCId = c.RelatedNCId,
            ResolutionEvidence = c.ResolutionEvidence,
            EffectivenessDate = c.EffectivenessDate,
            EffectivenessVerifiedBy = c.EffectivenessVerifiedBy,
            EffectivenessNotes = c.EffectivenessNotes,
            InvestigationResult = c.InvestigationResult,
            CorrectiveAction = c.CorrectiveAction,
            Status = (SharedModels.ComplaintStatus)c.Status,
            ClosedAt = c.ClosedAt,
            ClosedByUserId = c.ClosedByUserId,
            ClosedByUserName = closedByName,
            RowVersion = c.RowVersion,
            Actions = c.Actions.Select(a => new SharedModels.ComplaintAction
            {
                Id = a.Id,
                ComplaintId = a.ComplaintId,
                ActionType = (SharedModels.ComplaintActionType)a.ActionType,
                Description = a.Description,
                OwnerUserId = a.OwnerUserId,
                DueDate = a.DueDate,
                CompletedDate = a.CompletedDate,
                Status = (SharedModels.ActionStatus)a.Status
            }).ToList()
        };
    }

    public async Task<Guid> CreateComplaintAsync(CreateComplaintRequest request, Guid? userId = null, string? userName = null)
    {
        var complaint = new DomainEntities.Complaint
        {
            Id = Guid.NewGuid(),
            Source = request.Source,
            Description = request.Description,
            Category = (DomainEntities.ComplaintCategory)request.Category,
            InvestigationResult = request.InvestigationResult,
            CorrectiveAction = request.CorrectiveAction,
            ResolutionEvidence = request.ResolutionEvidence,
            Date = DateTime.UtcNow,
            Status = DomainEntities.ComplaintStatus.OPEN
        };

        _context.Complaints.Add(complaint);
        await _context.SaveChangesAsync();

        await LogAuditAsync("CREATE", "Complaint", complaint.Id, $"Queja registrada de '{complaint.Source}'", userId, userName);
        await _context.SaveChangesAsync();

        return complaint.Id;
    }

    public async Task<bool> UpdateComplaintAsync(Guid id, CreateComplaintRequest request, Guid? userId = null, string? userName = null)
    {
        var complaint = await _context.Complaints.FindAsync(id);
        if (complaint == null || complaint.IsDeleted) return false;

        complaint.Source = request.Source;
        complaint.Description = request.Description;
        complaint.Category = (DomainEntities.ComplaintCategory)request.Category;
        complaint.InvestigationResult = request.InvestigationResult;
        complaint.CorrectiveAction = request.CorrectiveAction;
        complaint.ResolutionEvidence = request.ResolutionEvidence;

        await LogAuditAsync("EDIT", "Complaint", complaint.Id, $"Queja de '{complaint.Source}' editada", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateComplaintStatusAsync(Guid id, SharedModels.ComplaintStatus status, Guid? userId = null, string? userName = null)
    {
        var complaint = await _context.Complaints.FindAsync(id);
        if (complaint == null || complaint.IsDeleted) return false;

        if (status == SharedModels.ComplaintStatus.CLOSED)
        {
            if (string.IsNullOrWhiteSpace(complaint.InvestigationResult))
            {
                throw new InvalidOperationException("No se puede cerrar la queja sin registrar el resultado de la investigación (ISO 15189 §7.7).");
            }

            if (string.IsNullOrWhiteSpace(complaint.CorrectiveAction))
            {
                throw new InvalidOperationException("No se puede cerrar la queja sin registrar la acción correctiva.");
            }

            if (string.IsNullOrWhiteSpace(complaint.ResolutionEvidence))
            {
                throw new InvalidOperationException("No se puede cerrar la queja sin registrar la evidencia de resolución (comunicación al cliente).");
            }

            complaint.ClosedAt = DateTime.UtcNow;
            complaint.ClosedByUserId = userId;
        }
        else
        {
            complaint.ClosedAt = null;
            complaint.ClosedByUserId = null;
        }

        complaint.Status = (DomainEntities.ComplaintStatus)status;

        await LogAuditAsync("STATUS_CHANGE", "Complaint", complaint.Id, $"Queja estado cambiado a {status}", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteComplaintAsync(Guid id, Guid? userId = null, string? userName = null)
    {
        var complaint = await _context.Complaints.FindAsync(id);
        if (complaint == null || complaint.IsDeleted) return false;

        complaint.IsDeleted = true;
        complaint.DeletedAt = DateTime.UtcNow;
        complaint.DeletedByUserId = userId;

        await LogAuditAsync("SOFT_DELETE", "Complaint", complaint.Id, $"Queja de '{complaint.Source}' eliminada lógicamente (conservación de evidencia histórica - ISO 15189 §8.7)", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Audit Logging Helper ──────────────────────────────────────────

    private void ValidateNCClosure(DomainEntities.Nonconformity nc)
    {
        if (string.IsNullOrWhiteSpace(nc.RootCauseAnalysis))
        {
            throw new InvalidOperationException("No se puede cerrar la No Conformidad sin registrar el Análisis de Causa Raíz (ISO 15189 §8.7.2).");
        }

        if (string.IsNullOrWhiteSpace(nc.Containment))
        {
            throw new InvalidOperationException("No se puede cerrar la No Conformidad sin registrar la Contención Inmediata.");
        }

        var activeActions = nc.Actions.Where(a => !a.IsDeleted && a.Status != DomainEntities.CAPAStatus.CANCELLED).ToList();
        if (!activeActions.Any())
        {
            throw new InvalidOperationException("Debe registrar al menos una acción CAPA antes de cerrar la No Conformidad.");
        }

        if (activeActions.Any(a => a.Status != DomainEntities.CAPAStatus.VERIFIED))
        {
            throw new InvalidOperationException("No se puede cerrar la No Conformidad: existen acciones CAPA que no han sido verificadas independientemente (estado VERIFIED).");
        }
    }

    private async Task LogAuditAsync(string action, string entityType, Guid? entityId, string details, Guid? userId, string? username)
    {
        var audit = new DomainEntities.AuditLog
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
