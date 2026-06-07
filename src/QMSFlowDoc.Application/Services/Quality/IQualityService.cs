using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QMSFlowDoc.Application.Services.Quality;

public interface IQualityService
{
    // Non-Conformities
    Task<List<NCListDto>> GetNonconformitiesAsync();
    Task<Nonconformity?> GetNCByIdAsync(Guid id);
    Task<Guid> CreateNCAsync(CreateNCRequest request, Guid? userId = null, string? userName = null);
    Task<bool> UpdateNCAsync(Guid id, CreateNCRequest request, Guid? userId = null, string? userName = null);
    Task<bool> UpdateNCStatusAsync(Guid id, NCStatus status, Guid? userId = null, string? userName = null);
    Task<bool> DeleteNCAsync(Guid id, Guid? userId = null, string? userName = null);

    // CAPA Actions
    Task<Guid> CreateCAPAAsync(CreateCAPARequest request, Guid? userId = null, string? userName = null);
    Task<bool> UpdateCAPAStatusAsync(Guid id, CAPAStatus status, Guid? userId = null, string? userName = null);
    Task<bool> CompleteCAPAAsync(Guid id, string? effectivenessCheck, Guid? userId = null, string? userName = null);
    Task<bool> VerifyCAPAAsync(Guid id, string? verificationNotes, Guid? userId = null, string? userName = null);
    Task<bool> DeleteCAPAAsync(Guid id, Guid? userId = null, string? userName = null);

    // Complaints
    Task<List<ComplaintListDto>> GetComplaintsAsync();
    Task<Complaint?> GetComplaintByIdAsync(Guid id);
    Task<Guid> CreateComplaintAsync(CreateComplaintRequest request, Guid? userId = null, string? userName = null);
    Task<bool> UpdateComplaintAsync(Guid id, CreateComplaintRequest request, Guid? userId = null, string? userName = null);
    Task<bool> UpdateComplaintStatusAsync(Guid id, ComplaintStatus status, Guid? userId = null, string? userName = null);
    Task<bool> DeleteComplaintAsync(Guid id, Guid? userId = null, string? userName = null);
}
