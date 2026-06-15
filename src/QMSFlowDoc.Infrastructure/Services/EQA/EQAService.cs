using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Application.Services.EQA;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Infrastructure.Services.EQA;

public class EQAService : IEQAService
{
    private readonly QmsDbContext _context;

    public EQAService(QmsDbContext context)
    {
        _context = context;
    }

    // ── Programs ─────────────────────────────────────────────────────

    public async Task<List<EQAProgram>> GetProgramsAsync()
    {
        return await _context.EQAPrograms
            .Where(p => !p.IsDeleted)
            .Include(p => p.Enrollments.Where(e => !e.IsDeleted))
            .Include(p => p.TestMappings.Where(m => !m.IsDeleted))
            .OrderBy(p => p.InternalCode)
            .ToListAsync();
    }

    public async Task<EQAProgram?> GetProgramByIdAsync(Guid id)
    {
        return await _context.EQAPrograms
            .Include(p => p.Enrollments.Where(e => !e.IsDeleted))
            .Include(p => p.TestMappings.Where(m => !m.IsDeleted))
            .Include(p => p.Rounds.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Samples)
            .Include(p => p.Rounds.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Deviations.Where(d => !d.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<Guid> CreateProgramAsync(EQAProgram program, Guid? userId = null, string? userName = null)
    {
        if (program.Id == Guid.Empty) program.Id = Guid.NewGuid();
        program.IsDeleted = false;
        
        _context.EQAPrograms.Add(program);
        await _context.SaveChangesAsync();

        await LogAuditAsync("CREATE", "EQAProgram", program.Id, $"Programa EQA '{program.Name}' ({program.InternalCode}) creado", userId, userName);
        await _context.SaveChangesAsync();

        return program.Id;
    }

    public async Task<bool> UpdateProgramAsync(EQAProgram program, Guid? userId = null, string? userName = null)
    {
        var existing = await _context.EQAPrograms.FirstOrDefaultAsync(p => p.Id == program.Id && !p.IsDeleted);
        if (existing == null) return false;

        _context.Entry(existing).CurrentValues.SetValues(program);
        await LogAuditAsync("EDIT", "EQAProgram", program.Id, $"Programa EQA '{program.Name}' modificado", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteProgramAsync(Guid id, Guid? userId = null, string? userName = null)
    {
        var program = await _context.EQAPrograms.FindAsync(id);
        if (program == null || program.IsDeleted) return false;

        program.IsDeleted = true;
        program.DeletedAt = DateTime.UtcNow;
        program.DeletedByUserId = userId;

        // Cascade soft delete to associated enrollments, rounds, mappings
        var enrollments = await _context.EQAEnrollments.Where(e => e.ProgramId == id && !e.IsDeleted).ToListAsync();
        foreach (var e in enrollments)
        {
            e.IsDeleted = true;
            e.DeletedAt = DateTime.UtcNow;
            e.DeletedByUserId = userId;
        }

        var mappings = await _context.EQAMappings.Where(m => m.ProgramId == id && !m.IsDeleted).ToListAsync();
        foreach (var m in mappings)
        {
            m.IsDeleted = true;
            m.DeletedAt = DateTime.UtcNow;
            m.DeletedByUserId = userId;
        }

        var rounds = await _context.EQARounds.Where(r => r.ProgramId == id && !r.IsDeleted).ToListAsync();
        foreach (var r in rounds)
        {
            r.IsDeleted = true;
            r.DeletedAt = DateTime.UtcNow;
            r.DeletedByUserId = userId;
        }

        var roundIds = rounds.Select(r => r.Id).ToList();
        var devs = await _context.EQADeviations.Where(d => roundIds.Contains(d.RoundId) && !d.IsDeleted).ToListAsync();
        foreach (var d in devs)
        {
            d.IsDeleted = true;
            d.DeletedAt = DateTime.UtcNow;
            d.DeletedByUserId = userId;
        }

        await LogAuditAsync("DELETE", "EQAProgram", id, $"Programa EQA '{program.Name}' eliminado lógicamente con cascada", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Enrollments ──────────────────────────────────────────────────

    public async Task<List<EQAEnrollment>> GetEnrollmentsAsync(int? year = null)
    {
        var query = _context.EQAEnrollments.Where(e => !e.IsDeleted);
        if (year.HasValue)
        {
            query = query.Where(e => e.Year == year.Value);
        }
        return await query.ToListAsync();
    }
    
    public async Task<EQAEnrollment?> GetEnrollmentByIdAsync(Guid id)
    {
        return await _context.EQAEnrollments.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
    }

    public async Task<Guid> CreateEnrollmentAsync(EQAEnrollment enrollment, Guid? userId = null, string? userName = null)
    {
        if (enrollment.Id == Guid.Empty) enrollment.Id = Guid.NewGuid();
        enrollment.IsDeleted = false;

        _context.EQAEnrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        await LogAuditAsync("CREATE", "EQAEnrollment", enrollment.Id, $"Inscripción EQA para el año {enrollment.Year}", userId, userName);
        await _context.SaveChangesAsync();

        return enrollment.Id;
    }

    public async Task<bool> UpdateEnrollmentAsync(EQAEnrollment enrollment, Guid? userId = null, string? userName = null)
    {
        var existing = await _context.EQAEnrollments.FirstOrDefaultAsync(e => e.Id == enrollment.Id && !e.IsDeleted);
        if (existing == null) return false;

        _context.Entry(existing).CurrentValues.SetValues(enrollment);
        await LogAuditAsync("EDIT", "EQAEnrollment", enrollment.Id, $"Inscripción EQA para el año {enrollment.Year} modificada", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteEnrollmentAsync(Guid id, Guid? userId = null, string? userName = null)
    {
        var enrollment = await _context.EQAEnrollments.FindAsync(id);
        if (enrollment == null || enrollment.IsDeleted) return false;

        enrollment.IsDeleted = true;
        enrollment.DeletedAt = DateTime.UtcNow;
        enrollment.DeletedByUserId = userId;

        await LogAuditAsync("DELETE", "EQAEnrollment", id, $"Inscripción EQA del año {enrollment.Year} eliminada lógicamente", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Mappings ─────────────────────────────────────────────────────

    public async Task<List<EQAMapping>> GetMappingsAsync(Guid programId)
    {
        return await _context.EQAMappings
            .Where(m => m.ProgramId == programId && !m.IsDeleted)
            .ToListAsync();
    }

    public async Task<Guid> SaveMappingAsync(EQAMapping mapping, Guid? userId = null, string? userName = null)
    {
        if (mapping.Id == Guid.Empty) mapping.Id = Guid.NewGuid();
        mapping.IsDeleted = false;

        var existing = await _context.EQAMappings.FirstOrDefaultAsync(m => m.Id == mapping.Id && !m.IsDeleted);
        
        if (existing == null)
        {
            _context.EQAMappings.Add(mapping);
            await LogAuditAsync("CREATE", "EQAMapping", mapping.Id, $"Mapeo programa-prueba creado para '{mapping.InternalTestName}'", userId, userName);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(mapping);
            await LogAuditAsync("EDIT", "EQAMapping", mapping.Id, $"Mapeo programa-prueba actualizado para '{mapping.InternalTestName}'", userId, userName);
        }

        await _context.SaveChangesAsync();
        return mapping.Id;
    }

    public async Task<bool> DeleteMappingAsync(Guid id, Guid? userId = null, string? userName = null)
    {
        var mapping = await _context.EQAMappings.FindAsync(id);
        if (mapping == null || mapping.IsDeleted) return false;

        mapping.IsDeleted = true;
        mapping.DeletedAt = DateTime.UtcNow;
        mapping.DeletedByUserId = userId;

        await LogAuditAsync("DELETE", "EQAMapping", id, $"Mapeo de la prueba '{mapping.InternalTestName}' eliminado lógicamente", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Rounds ───────────────────────────────────────────────────────

    public async Task<List<EQARound>> GetRoundsAsync(int? year = null)
    {
        var query = _context.EQARounds
            .Where(r => !r.IsDeleted)
            .Include(r => r.Samples)
            .Include(r => r.Deviations.Where(d => !d.IsDeleted))
            .AsQueryable();

        if (year.HasValue)
        {
            query = query.Where(r => r.Year == year.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<EQARound?> GetRoundByIdAsync(Guid id)
    {
        return await _context.EQARounds
            .Include(r => r.Samples)
            .Include(r => r.Deviations.Where(d => !d.IsDeleted))
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }

    public async Task<Guid> CreateRoundAsync(EQARound round, Guid? userId = null, string? userName = null)
    {
        if (round.Id == Guid.Empty) round.Id = Guid.NewGuid();
        round.IsDeleted = false;
        
        // Also ensure sample IDs are initialized
        foreach (var sample in round.Samples)
        {
            if (sample.Id == Guid.Empty) sample.Id = Guid.NewGuid();
            sample.RoundId = round.Id;
        }

        _context.EQARounds.Add(round);
        await _context.SaveChangesAsync();

        await LogAuditAsync("CREATE", "EQARound", round.Id, $"Ronda EQA '{round.ExternalCode}' creada", userId, userName);
        await _context.SaveChangesAsync();

        return round.Id;
    }

    public async Task<bool> UpdateRoundAsync(EQARound round, Guid? userId = null, string? userName = null)
    {
        // Enforce validations
        if (round.GlobalOutcome == EQAPerformance.UNSATISFACTORY)
        {
            round.RequiresAction = true;
            var activeDeviations = round.Deviations.Where(d => !d.IsDeleted).ToList();
            if (!activeDeviations.Any())
            {
                throw new InvalidOperationException("Un resultado insatisfactorio (UNSATISFACTORY) requiere registrar al menos una desviación activa en la ronda.");
            }
        }

        if (round.Status == EQARoundStatus.CLOSED)
        {
            var activeDeviations = round.Deviations.Where(d => !d.IsDeleted).ToList();
            if (round.GlobalOutcome == EQAPerformance.UNSATISFACTORY)
            {
                foreach (var dev in activeDeviations)
                {
                    if (dev.Status != "Cerrada")
                    {
                        throw new InvalidOperationException("No se puede cerrar la ronda EQA porque existen desviaciones asociadas que no están en estado 'Cerrada'.");
                    }
                    if (dev.LinkedCapaId == null || dev.LinkedCapaId == Guid.Empty)
                    {
                        throw new InvalidOperationException("No se puede cerrar la ronda EQA porque existen desviaciones que no están vinculadas a una NC o CAPA.");
                    }
                    var linkedId = dev.LinkedCapaId.Value;
                    bool isLinkedClosedNc = await _context.Nonconformities.AnyAsync(nc => nc.Id == linkedId && nc.Status == NCStatus.CLOSED && !nc.IsDeleted);
                    bool isLinkedVerifiedCapa = await _context.CapaActions.AnyAsync(capa => capa.Id == linkedId && capa.Status == CAPAStatus.VERIFIED && !capa.IsDeleted);
                    if (!isLinkedClosedNc && !isLinkedVerifiedCapa)
                    {
                        throw new InvalidOperationException("La No Conformidad o CAPA vinculada a la desviación debe estar en estado CLOSED o VERIFIED para poder cerrar la ronda.");
                    }
                }
            }
        }

        var existing = await _context.EQARounds
            .Include(r => r.Samples)
            .Include(r => r.Deviations)
            .FirstOrDefaultAsync(r => r.Id == round.Id && !r.IsDeleted);

        if (existing == null) return false;

        var detailsMsg = $"Ronda EQA '{round.ExternalCode}' modificada (Estado: {round.Status})";
        
        // Update scalar values
        _context.Entry(existing).CurrentValues.SetValues(round);

        // Update Samples collection (adds, edits, removes)
        foreach (var sample in round.Samples)
        {
            var existingSample = existing.Samples.FirstOrDefault(s => s.Id == sample.Id);
            if (existingSample == null)
            {
                if (sample.Id == Guid.Empty) sample.Id = Guid.NewGuid();
                sample.RoundId = round.Id;
                existing.Samples.Add(sample);
            }
            else
            {
                _context.Entry(existingSample).CurrentValues.SetValues(sample);
            }
        }
        
        // Remove samples not in new list
        var sampleIds = round.Samples.Select(s => s.Id).ToList();
        var samplesToRemove = existing.Samples.Where(s => !sampleIds.Contains(s.Id)).ToList();
        foreach (var sample in samplesToRemove)
        {
            existing.Samples.Remove(sample);
        }

        // Update Deviations collection
        foreach (var deviation in round.Deviations)
        {
            var existingDev = existing.Deviations.FirstOrDefault(d => d.Id == deviation.Id);
            if (existingDev == null)
            {
                if (deviation.Id == Guid.Empty) deviation.Id = Guid.NewGuid();
                deviation.RoundId = round.Id;
                deviation.IsDeleted = false;
                existing.Deviations.Add(deviation);
            }
            else
            {
                _context.Entry(existingDev).CurrentValues.SetValues(deviation);
            }
        }

        var devIds = round.Deviations.Select(d => d.Id).ToList();
        var devsToRemove = existing.Deviations.Where(d => !devIds.Contains(d.Id)).ToList();
        foreach (var dev in devsToRemove)
        {
            // Soft delete removed deviations
            dev.IsDeleted = true;
            dev.DeletedAt = DateTime.UtcNow;
            dev.DeletedByUserId = userId;
        }

        await LogAuditAsync("EDIT", "EQARound", round.Id, detailsMsg, userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteRoundAsync(Guid id, Guid? userId = null, string? userName = null)
    {
        var round = await _context.EQARounds.FindAsync(id);
        if (round == null || round.IsDeleted) return false;

        round.IsDeleted = true;
        round.DeletedAt = DateTime.UtcNow;
        round.DeletedByUserId = userId;

        // Cascade soft delete deviations in this round
        var devs = await _context.EQADeviations.Where(d => d.RoundId == id && !d.IsDeleted).ToListAsync();
        foreach (var d in devs)
        {
            d.IsDeleted = true;
            d.DeletedAt = DateTime.UtcNow;
            d.DeletedByUserId = userId;
        }

        await LogAuditAsync("DELETE", "EQARound", id, $"Ronda EQA '{round.ExternalCode}' eliminada lógicamente", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Samples ──────────────────────────────────────────────────────

    public async Task<EQASample?> GetSampleByIdAsync(Guid id)
    {
        return await _context.EQASamples.FindAsync(id);
    }

    public async Task<bool> UpdateSampleAsync(EQASample sample, Guid? userId = null, string? userName = null)
    {
        var existing = await _context.EQASamples.FindAsync(sample.Id);
        if (existing == null) return false;

        _context.Entry(existing).CurrentValues.SetValues(sample);
        await LogAuditAsync("EDIT", "EQASample", sample.Id, $"Muestra EQA '{sample.InternalCode}' (Procesamiento/Resultados) actualizada", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Deviations ───────────────────────────────────────────────────

    public async Task<List<EQADeviation>> GetDeviationsAsync()
    {
        return await _context.EQADeviations
            .Where(d => !d.IsDeleted)
            .OrderByDescending(d => d.Id)
            .ToListAsync();
    }

    public async Task<Guid> CreateDeviationAsync(EQADeviation deviation, Guid? userId = null, string? userName = null)
    {
        if (deviation.Id == Guid.Empty) deviation.Id = Guid.NewGuid();
        deviation.IsDeleted = false;

        _context.EQADeviations.Add(deviation);
        await _context.SaveChangesAsync();

        await LogAuditAsync("CREATE", "EQADeviation", deviation.Id, $"Desviación EQA registrada (Tipo: {deviation.DeviationType})", userId, userName);
        await _context.SaveChangesAsync();

        return deviation.Id;
    }

    public async Task<bool> UpdateDeviationAsync(EQADeviation deviation, Guid? userId = null, string? userName = null)
    {
        var existing = await _context.EQADeviations.FirstOrDefaultAsync(d => d.Id == deviation.Id && !d.IsDeleted);
        if (existing == null) return false;

        _context.Entry(existing).CurrentValues.SetValues(deviation);
        await LogAuditAsync("EDIT", "EQADeviation", deviation.Id, $"Desviación EQA '{deviation.Id}' actualizada (Estado: {deviation.Status})", userId, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Audit Logging Helper ──────────────────────────────────────────

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
}
