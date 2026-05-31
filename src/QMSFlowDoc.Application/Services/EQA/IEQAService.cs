using QMSFlowDoc.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QMSFlowDoc.Application.Services.EQA;

public interface IEQAService
{
    // Programs
    Task<List<EQAProgram>> GetProgramsAsync();
    Task<EQAProgram?> GetProgramByIdAsync(Guid id);
    Task<Guid> CreateProgramAsync(EQAProgram program, Guid? userId = null, string? userName = null);
    Task<bool> UpdateProgramAsync(EQAProgram program, Guid? userId = null, string? userName = null);
    Task<bool> DeleteProgramAsync(Guid id, Guid? userId = null, string? userName = null);

    // Enrollments
    Task<List<EQAEnrollment>> GetEnrollmentsAsync(int? year = null);
    Task<EQAEnrollment?> GetEnrollmentByIdAsync(Guid id);
    Task<Guid> CreateEnrollmentAsync(EQAEnrollment enrollment, Guid? userId = null, string? userName = null);
    Task<bool> UpdateEnrollmentAsync(EQAEnrollment enrollment, Guid? userId = null, string? userName = null);
    Task<bool> DeleteEnrollmentAsync(Guid id, Guid? userId = null, string? userName = null);

    // Mappings
    Task<List<EQAMapping>> GetMappingsAsync(Guid programId);
    Task<Guid> SaveMappingAsync(EQAMapping mapping, Guid? userId = null, string? userName = null);
    Task<bool> DeleteMappingAsync(Guid id, Guid? userId = null, string? userName = null);

    // Rounds
    Task<List<EQARound>> GetRoundsAsync(int? year = null);
    Task<EQARound?> GetRoundByIdAsync(Guid id);
    Task<Guid> CreateRoundAsync(EQARound round, Guid? userId = null, string? userName = null);
    Task<bool> UpdateRoundAsync(EQARound round, Guid? userId = null, string? userName = null);
    Task<bool> DeleteRoundAsync(Guid id, Guid? userId = null, string? userName = null);

    // Samples
    Task<EQASample?> GetSampleByIdAsync(Guid id);
    Task<bool> UpdateSampleAsync(EQASample sample, Guid? userId = null, string? userName = null);

    // Deviations & CAPA
    Task<List<EQADeviation>> GetDeviationsAsync();
    Task<Guid> CreateDeviationAsync(EQADeviation deviation, Guid? userId = null, string? userName = null);
    Task<bool> UpdateDeviationAsync(EQADeviation deviation, Guid? userId = null, string? userName = null);
}
