using System;

namespace QMSFlowDoc.Shared.DTOs;

public class CompetencyDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int? RequiredFrequencyMonths { get; set; }
}

public class CompetencyEvaluationDto
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public Guid? CompetencyId { get; set; }
    public string CompetencyName { get; set; } = string.Empty;
    public string? Area { get; set; }
    public DateTime EvaluationDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string Outcome { get; set; } = "Pending";
    public string? Evidence { get; set; }
    public string? EvaluatorName { get; set; }
    public Guid? EvaluatorStaffId { get; set; }
}
