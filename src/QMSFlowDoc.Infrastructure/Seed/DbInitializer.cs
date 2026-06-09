using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QMSFlowDoc.Domain.Identity;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Infrastructure.Seed
{
    public static class DbInitializer
    {
        private const string PreviousIsoImprovementMigrationId = "20260606190714_AddCanApproveToRolePermission";
        private const string IsoImprovementMigrationId = "20260607134000_AddIsoImprovementControls";
        private const string EfProductVersion = "9.0.0";

        public static async Task SeedIdentityAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<QmsDbContext>();
            
            // 1. Apply EF migrations to target database
            if (IsSqlite(context))
            {
                if (!await IsMigrationAppliedAsync(context, IsoImprovementMigrationId))
                {
                    await context.Database.MigrateAsync(PreviousIsoImprovementMigrationId);
                    await EnsureIsoImprovementSqliteSchemaAsync(context);
                    await MarkMigrationAppliedAsync(context, IsoImprovementMigrationId, EfProductVersion);
                }

                await context.Database.MigrateAsync();
                await EnsureIsoImprovementSqliteSchemaAsync(context);
            }
            else
            {
                await context.Database.MigrateAsync();
            }

            // Fix invalid Guid values in database from older legacy seeds
            try
            {
                await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
                await context.Database.ExecuteSqlRawAsync("UPDATE StaffAuthorizations SET AuthorizationId = 'e1c0de00-0000-0000-0000-000000000001' WHERE AuthorizationId = 'auth-encender';");
                await context.Database.ExecuteSqlRawAsync("UPDATE StaffAuthorizations SET AuthorizationId = 'e1c0de00-0000-0000-0000-000000000002' WHERE AuthorizationId = 'auth-apagar';");
                await context.Database.ExecuteSqlRawAsync("UPDATE AuthorizationCatalogs SET Id = 'e1c0de00-0000-0000-0000-000000000001' WHERE Id = 'auth-encender';");
                await context.Database.ExecuteSqlRawAsync("UPDATE AuthorizationCatalogs SET Id = 'e1c0de00-0000-0000-0000-000000000002' WHERE Id = 'auth-apagar';");
                await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Warning cleaning legacy Guid values: {ex.Message}");
            }

            var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();

            // 1. Seed Default Roles (needed if no legacy DB, or as fallback)
            string[] roleNames = { "Administrador", "Facultativo", "Técnico", "Responsable calidad", "Auditor" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new ApplicationRole(roleName)
                    {
                        Description = $"Rol de {roleName}"
                    });
                }
            }

            // 2. Map and cleanup legacy roles if they exist
            var legacyMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Staff", "Técnico" },
                { "Quality", "Responsable calidad" },
                { "Consultor", "Auditor" },
                { "Manager", "Responsable calidad" }
            };

            foreach (var mapping in legacyMappings)
            {
                var legacyRoleName = mapping.Key;
                var targetRoleName = mapping.Value;

                var legacyRole = await roleManager.FindByNameAsync(legacyRoleName);
                if (legacyRole != null)
                {
                    // Map all users in the legacy role to the new role
                    var usersInLegacyRole = await userManager.GetUsersInRoleAsync(legacyRoleName);
                    foreach (var user in usersInLegacyRole)
                    {
                        if (!await userManager.IsInRoleAsync(user, targetRoleName))
                        {
                            await userManager.AddToRoleAsync(user, targetRoleName);
                        }
                        await userManager.RemoveFromRoleAsync(user, legacyRoleName);
                    }

                    // Delete associated permissions in RolePermissions table
                    var rpEntries = await context.RolePermissions.Where(rp => rp.RoleId == legacyRole.Id).ToListAsync();
                    if (rpEntries.Any())
                    {
                        context.RolePermissions.RemoveRange(rpEntries);
                    }

                    // Delete the legacy role
                    await roleManager.DeleteAsync(legacyRole);
                }
            }
            await context.SaveChangesAsync();

            // 3. Check for automatic legacy database migration
            var legacyDbPath = config["LegacyMigration:SourceDbPath"];
            
            if (!string.IsNullOrWhiteSpace(legacyDbPath) && File.Exists(legacyDbPath))
            {
                int currentUsersCount = await context.Users.CountAsync();
                
                // If the new DB only has 0 or 1 user (admin), run migration from the legacy DB
                if (currentUsersCount <= 1)
                {
                    Console.WriteLine("Legacy database found. Initiating automatic migration to clean architecture...");
                    try
                    {
                        // Attach legacy database
                        await context.Database.ExecuteSqlRawAsync($"ATTACH '{legacyDbPath}' AS legacy;");

                        // 3.1 Migrate Roles
                        try
                        {
                            await context.Database.ExecuteSqlRawAsync(@"
                                INSERT OR IGNORE INTO Roles (Id, Name, NormalizedName, ConcurrencyStamp, Description)
                                SELECT Id, RoleName, UPPER(RoleName), 'MIGRATED_CONCURRENCY_STAMP', Description
                                FROM legacy.Roles
                                WHERE UPPER(RoleName) NOT IN ('STAFF', 'QUALITY', 'CONSULTOR', 'MANAGER');");
                            Console.WriteLine("✔ Migrated Roles successfully (filtered legacy roles).");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠ Warning migrating Roles: {ex.Message}");
                        }

                        // 3.2 Migrate Users (maintaining BCrypt PasswordHash for MigrationPasswordHasher)
                        try
                        {
                            await context.Database.ExecuteSqlRawAsync(@"
                                INSERT OR IGNORE INTO Users (
                                    Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, 
                                    PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, 
                                    TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount, FullName, IsActive, CreatedAt
                                )
                                SELECT 
                                    Id, Username, UPPER(Username), Email, UPPER(Email), 1, 
                                    PasswordHash, 'MIGRATED_SECURITY_STAMP', 'MIGRATED_CONCURRENCY_STAMP', NULL, 0, 
                                    0, LockedUntil, 1, FailedLoginAttempts, FullName, IsActive, CreatedAt
                                FROM legacy.Users;");
                            Console.WriteLine("✔ Migrated Users successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠ Warning migrating Users: {ex.Message}");
                        }

                        // 3.3 Migrate UserRoles (mapping legacy roles to target roles by name to resolve FK constraints)
                        try
                        {
                            await context.Database.ExecuteSqlRawAsync(@"
                                INSERT OR IGNORE INTO UserRoles (UserId, RoleId)
                                SELECT ur.UserId, r_target.Id
                                FROM legacy.UserRoles ur
                                JOIN legacy.Roles r_legacy ON ur.RoleId = r_legacy.Id
                                JOIN Roles r_target ON r_target.NormalizedName = 
                                    CASE UPPER(r_legacy.RoleName)
                                        WHEN 'STAFF' THEN 'TÉCNICO'
                                        WHEN 'QUALITY' THEN 'RESPONSABLE CALIDAD'
                                        WHEN 'CONSULTOR' THEN 'AUDITOR'
                                        WHEN 'MANAGER' THEN 'RESPONSABLE CALIDAD'
                                        ELSE UPPER(r_legacy.RoleName)
                                    END;");
                            Console.WriteLine("✔ Migrated UserRoles successfully with role mapping.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠ Warning migrating UserRoles: {ex.Message}");
                        }

                        // 3.4 Migrate all other data tables in dependency order with explicit columns and mappings
                        var customMigrations = new List<(string Table, string Sql)>
                        {
                            ("Folders", "INSERT OR IGNORE INTO Folders (Id, Name, ParentFolderId, CreatedAt) SELECT Id, Name, ParentFolderId, CreatedAt FROM legacy.Folders;"),
                            
                            ("DocumentTypes", "INSERT OR IGNORE INTO DocumentTypes (Id, TypeCode, Name, Description) SELECT Id, TypeCode, Name, Description FROM legacy.DocumentTypes;"),
                            
                            ("Documents", "INSERT OR IGNORE INTO Documents (Id, DocCode, Title, DocumentTypeId, FolderId, Area, Process, OwnerUserId, Status, ReviewIntervalMonths, NextReviewDue, CreatedAt, UpdatedAt, IsDeleted, RowVersion) SELECT Id, DocCode, Title, DocumentTypeId, FolderId, Area, Process, OwnerUserId, Status, ReviewIntervalMonths, NextReviewDue, CreatedAt, UpdatedAt, COALESCE(IsDeleted, 0), RowVersion FROM legacy.Documents;"),
                            
                            ("DocumentVersions", "INSERT OR IGNORE INTO DocumentVersions (Id, DocumentId, VersionMajor, VersionMinor, VersionLabel, ChangeSummary, CreatedByUserId, CreatedAt, EffectiveFrom, Sha256, MimeType, FileName, RelativePath, IsCurrent, RowVersion, ApprovedByUserId, ApprovalDate) SELECT Id, DocumentId, VersionMajor, VersionMinor, VersionLabel, ChangeSummary, CreatedByUserId, CreatedAt, EffectiveFrom, Sha256, MimeType, FileName, LocalFilePath, COALESCE(IsCurrent, 1), RowVersion, NULL, NULL FROM legacy.DocumentVersions;"),
                            
                            ("Equipments", "INSERT OR IGNORE INTO Equipments (Id, InternalId, AssetTag, Name, Manufacturer, Model, SerialNumber, SoftwareVersion, FirmwareVersion, Location, Status, InstalledAt, Notes, ReceptionDate, ReceptionCondition, VerificationDate, IsVerified, CalibrationFrequencyMonths, LastCalibration, NextCalibration, ManualPath, IsDeleted, RowVersion) SELECT Id, InternalId, AssetTag, Name, Manufacturer, Model, SerialNumber, SoftwareVersion, FirmwareVersion, Location, Status, InstalledAt, Notes, ReceptionDate, ReceptionCondition, VerificationDate, COALESCE(IsVerified, 0), CalibrationFrequencyMonths, LastCalibration, NextCalibration, ManualPath, COALESCE(IsDeleted, 0), RowVersion FROM legacy.Equipments;"),
                            
                            ("MaintenancePlans", "INSERT OR IGNORE INTO MaintenancePlans (Id, EquipmentId, PlanName, FrequencyDays, ChecklistJson, IsActive) SELECT Id, EquipmentId, PlanName, FrequencyDays, ChecklistJson, IsActive FROM legacy.MaintenancePlans;"),
                            
                            ("MaintenanceEvents", "INSERT OR IGNORE INTO MaintenanceEvents (Id, EquipmentId, PlanId, PerformedAt, PerformedByUserId, EventType, Outcome, Notes, EvidenceDocId, HasIssues, NextMaintenanceMonth, NextMaintenanceYear, CertificatePath, Cost, IsEfficiencyCheck, RowVersion) SELECT Id, EquipmentId, PlanId, PerformedAt, PerformedByUserId, EventType, Outcome, Notes, NULL, HasIssues, NextMaintenanceMonth, NextMaintenanceYear, CertificatePath, Cost, COALESCE(IsEfficiencyCheck, 0), RowVersion FROM legacy.MaintenanceEvents;"),
                            
                            ("EquipmentHistory", "INSERT OR IGNORE INTO EquipmentHistory (Id, EquipmentId, Date, UserId, UserName, ActionType, Description, OldValue, NewValue) SELECT Id, EquipmentId, Date, UserId, UserName, ActionType, Description, OldValue, NewValue FROM legacy.EquipmentHistory;"),
                            
                            ("EquipmentDailyQC", "INSERT OR IGNORE INTO EquipmentDailyQC (Id, EquipmentId, LotNumber, IsPass, Notes, PerformedByUserId, PerformedAt) SELECT Id, EquipmentId, LotNumber, IsPass, Notes, PerformedByUserId, PerformedAt FROM legacy.EquipmentDailyQC;"),
                            
                            ("StorageLocations", "INSERT OR IGNORE INTO StorageLocations (Id, Name, Description) SELECT Id, Name, Description FROM legacy.StorageLocations;"),
                            
                            ("ReagentTypes", "INSERT OR IGNORE INTO ReagentTypes (Id, Name, Description) SELECT Id, Name, Description FROM legacy.ReagentTypes;"),
                            
                            ("Suppliers", "INSERT OR IGNORE INTO Suppliers (Id, Name, ContactName, Email, Phone, Address, Notes, Type, QualityStatus, LastEvaluationDate, NextEvaluationDate, CreatedAt, UpdatedAt, IsDeleted, RowVersion) SELECT Id, Name, ContactName, Email, Phone, Address, Notes, Type, QualityStatus, LastEvaluationDate, NextEvaluationDate, CreatedAt, UpdatedAt, 0, RowVersion FROM legacy.Suppliers;"),
                            
                            ("Reagents", "INSERT OR IGNORE INTO Reagents (Id, Name, Manufacturer, SupplierId, ManufacturerCode, InternalCode, Fluorescence, ReagentType, Reference, Classification, StorageConditions, DefaultLocationId, OpenShelfLifeDays, Status, MinStock, TargetStock, ReorderQty, CreatedAt, UpdatedAt, IsDeleted, RowVersion) SELECT Id, Name, Manufacturer, SupplierId, ManufacturerCode, InternalCode, Fluorescence, ReagentType, Reference, Classification, StorageConditions, DefaultLocationId, OpenShelfLifeDays, Status, MinStock, TargetStock, ReorderQty, CreatedAt, COALESCE(UpdatedAt, CreatedAt), IsDeleted, RowVersion FROM legacy.Reagents;"),
                            
                            ("ReagentLots", "INSERT OR IGNORE INTO ReagentLots (Id, ReagentId, LotNumber, ExpiryDate, ReceivedDate, ReceivedQty, AvailableQty, LocationId, Status, OpenedDate, OpenExpiryDate, PanelId, ReleaseByUserId, ReleaseAt, CreatedAt, RowVersion) SELECT Id, ReagentId, LotNumber, ExpiryDate, ReceivedDate, ReceivedQty, AvailableQty, LocationId, Status, OpenedDate, OpenExpiryDate, PanelId, ReleaseByUserId, NULL, ReceivedDate, RowVersion FROM legacy.ReagentLots;"),
                            
                            ("InventoryMovements", "INSERT OR IGNORE INTO InventoryMovements (Id, MovedAt, UserId, ReagentId, ReagentLotId, MovementType, Qty, Reason, ReferenceType, ReferenceId, Notes) SELECT Id, Date, UserId, ReagentId, LotId, COALESCE(Type, 0), Qty, 'Migración de inventario', NULL, NULL, Notes FROM legacy.InventoryMovements;"),
                            
                            ("SupplierEvaluations", "INSERT OR IGNORE INTO SupplierEvaluations (Id, SupplierId, EvaluationDate, EvaluatorUserId, EvaluatedPeriod, ScorePlazos, ScoreCalidad, ScoreServicio, ScoreIncidencias, IsApproved, Observations, AttachmentPath, CreatedAt) SELECT Id, SupplierId, EvaluationDate, EvaluatorUserId, EvaluatedPeriod, ScorePlazos, ScoreCalidad, ScoreServicio, ScoreIncidencias, IsApproved, Observations, AttachmentPath, CreatedAt FROM legacy.SupplierEvaluations;"),
                            
                            ("TrainingActivities", "INSERT OR IGNORE INTO TrainingActivities (Id, Title, Provider, TrainingTypeId, Modality, StartDate, EndDate, Hours, Credits, Description, IsInternal, InternalDepartment, Status, AnnulReason, CreatedByUserId, CreatedAt, UpdatedAt) SELECT Id, Title, Provider, TrainingTypeId, Modality, StartDate, EndDate, Hours, Credits, Description, IsInternal, InternalDepartment, Status, NULL, CreatedByUserId, CreatedAt, UpdatedAt FROM legacy.TrainingActivities;"),
                            
                            ("StaffProfiles", "INSERT OR IGNORE INTO StaffProfiles (Id, UserId, PositionTitle, Department, HiredAt, IsActive, RowVersion) SELECT Id, UserId, Position, Department, HireDate, IsActive, RowVersion FROM legacy.StaffProfiles;"),
                            
                            ("StaffTrainings", "INSERT OR IGNORE INTO StaffTrainings (Id, StaffId, TrainingActivityId, ParticipationRole, Result, Score, CompletionDate, CertificateDocId, Notes, Status, AnnulReason, CreatedAt) SELECT Id, StaffId, TrainingActivityId, 'Participante', Result, Score, CompletionDate, CertificatePath, Notes, Status, NULL, COALESCE(CompletionDate, CompletedAt, CURRENT_TIMESTAMP) FROM legacy.StaffTrainings;"),
                            
                            ("CompetencyCatalogsSeeding", "INSERT OR IGNORE INTO CompetencyCatalogs (Id, Code, Name, Description, RoleScope, Area, SubArea, DefaultReassessmentMonths, IsActive, CreatedAt, CreatedByUserId) SELECT Id, Code, Name, Description, 'Staff', Category, NULL, COALESCE(RequiredFrequencyMonths, 12), 1, CreatedAt, '00000000-0000-0000-0000-000000000000' FROM legacy.Competencies;"),
                            
                            ("CompetencyEvaluations", "INSERT OR IGNORE INTO CompetencyEvaluations (Id, StaffId, CompetencyId, TemplateId, EvaluationDate, EvaluatorStaffId, Outcome, ValidUntil, NextDueDate, Findings, CorrectiveActions, EvidenceDocId, Status, AnnulReason, CreatedAt) SELECT Id, StaffId, CompetencyId, NULL, EvaluationDate, '00000000-0000-0000-0000-000000000000', Outcome, ValidUntil, NULL, Evidence, NULL, NULL, 'Approved', NULL, COALESCE(EvaluationDate, CURRENT_TIMESTAMP) FROM legacy.CompetencyEvaluations;"),
                            
                            ("Competencies", "INSERT OR IGNORE INTO Competencies (Id, Code, Name, Description, Category, RequiredFrequencyMonths, CreatedAt, UpdatedAt) SELECT Id, Code, Name, Description, Category, RequiredFrequencyMonths, CreatedAt, UpdatedAt FROM legacy.Competencies;"),
                                          
                            ("AuthorizationCatalogsLegacySeeding", "INSERT OR IGNORE INTO AuthorizationCatalogs (Id, Code, Name, Description, RoleScope, RequiresCompetency, ValidityMonths, IsActive, CreatedAt) VALUES ('e1c0de00-0000-0000-0000-000000000001', 'AUTH-ENCENDER', 'Autorización para: Encender citometro', 'Migrado de legacy', 'Técnico', 0, 12, 1, '2026-02-11T19:25:41Z'), ('e1c0de00-0000-0000-0000-000000000002', 'AUTH-APAGAR', 'Autorización para: Apagar citometro', 'Migrado de legacy', 'Técnico', 0, 12, 1, '2026-02-11T19:25:41Z');"),
                             
                            ("StaffAuthorizations", "INSERT OR IGNORE INTO StaffAuthorizations (Id, StaffId, AuthorizationId, GrantedByUserId, GrantedAt, ValidFrom, ValidUntil, Status, RevocationReason, EvidenceDocId, CreatedAt, UpdatedAt, RowVersion) SELECT Id, StaffId, CASE WHEN TaskName LIKE '%Encender%' THEN 'e1c0de00-0000-0000-0000-000000000001' ELSE 'e1c0de00-0000-0000-0000-000000000002' END, GrantedByUserId, GrantedAt, ValidFrom, ValidUntil, Status, NULL, NULL, GrantedAt, GrantedAt, RowVersion FROM legacy.StaffAuthorizations;"),
                            
                            ("Nonconformities", "INSERT OR IGNORE INTO Nonconformities (Id, DetectedAt, DetectedByUserId, Title, Description, Severity, ImpactPatient, Containment, Origin, RootCauseAnalysis, Status, UpdatedAt, RowVersion) SELECT Id, DetectedAt, DetectedByUserId, Title, Description, Severity, ImpactPatient, Containment, Origin, RootCauseAnalysis, Status, UpdatedAt, RowVersion FROM legacy.Incidents;"),
                            
                            ("CapaActions", "INSERT OR IGNORE INTO CapaActions (Id, NCId, ActionType, Description, OwnerUserId, DueDate, CompletedAt, EffectivenessCheck, Status) SELECT Id, NCId, ActionType, Description, OwnerUserId, DueDate, CompletedAt, EffectivenessCheck, Status FROM legacy.IncidentActions;"),
                            
                            ("Complaints", "INSERT OR IGNORE INTO Complaints (Id, Date, Source, Description, Category, ClaimantType, IsSubstantiated, ReceiptDate, ReceiptMethod, ClinicalImpact, RelatedNCId, ResolutionEvidence, EffectivenessDate, EffectivenessVerifiedBy, EffectivenessNotes, InvestigationResult, CorrectiveAction, Status, ClosedAt, RowVersion) SELECT Id, Date, Source, Description, Category, ClaimantType, IsSubstantiated, ReceiptDate, ReceiptMethod, ClinicalImpact, RelatedNCId, ResolutionEvidence, EffectivenessDate, EffectivenessVerifiedBy, EffectivenessNotes, InvestigationResult, CorrectiveAction, Status, ClosedAt, RowVersion FROM legacy.Complaints;"),
                            
                            ("ComplaintActions", "INSERT OR IGNORE INTO ComplaintActions (Id, ComplaintId, ActionType, Description, OwnerUserId, DueDate, CompletedDate, Status) SELECT Id, ComplaintId, ActionType, Description, OwnerUserId, DueDate, CompletedDate, Status FROM legacy.ComplaintActions;"),
                            
                            ("Risks", "INSERT OR IGNORE INTO Risks (Id, Title, Description, Category, Likelihood, Impact, MitigationPlan, OwnerUserId, OwnerName, Status, CreatedAt, UpdatedAt, RowVersion) SELECT Id, Title, Description, Category, Likelihood, Impact, MitigationPlan, OwnerUserId, OwnerName, Status, CreatedAt, UpdatedAt, RowVersion FROM legacy.Risks;"),
                            
                            ("AuditPlans", "INSERT OR IGNORE INTO AuditPlans (Id, Title, ScheduledDate, Scope, LeadAuditor, Status, SummaryReport, ReportDocumentId, ChecklistJson, RowVersion) SELECT Id, Title, ScheduledDate, Scope, LeadAuditor, Status, SummaryReport, CASE WHEN ReportDocumentId IN (SELECT Id FROM Documents) THEN ReportDocumentId ELSE NULL END, ChecklistJson, RowVersion FROM legacy.AuditPlans;"),
                            
                            ("AuditFindings", "INSERT OR IGNORE INTO AuditFindings (Id, AuditPlanId, Description, IsoRequirement, Type, RelatedNCId) SELECT Id, AuditPlanId, Description, IsoRequirement, Type, RelatedNCId FROM legacy.AuditFindings;"),
                            
                            ("ManagementReviews", "INSERT OR IGNORE INTO ManagementReviews (Id, ReviewDate, Participants, Agenda, Summary, Actions, MinutesDocumentId) SELECT Id, ReviewDate, Participants, Agenda, Summary, Actions, CASE WHEN MinutesDocumentId IN (SELECT Id FROM Documents) THEN MinutesDocumentId ELSE NULL END FROM legacy.ManagementReviews;"),
                            
                            ("IQCResults", "INSERT OR IGNORE INTO IQCResults (Id, EquipmentName, AnalyteName, Level, Value, Mean, SD, Date, Status, WestgardRule, Comments) SELECT Id, EquipmentName, AnalyteName, Level, Value, Mean, SD, Date, Status, WestgardRule, Comments FROM legacy.IQCResults;"),
                            
                            ("ContingencyPlans", "INSERT OR IGNORE INTO ContingencyPlans (Id, Title, TriggerEvent, ProcedureSteps, ResponsiblePerson, LastReviewDate, Status) SELECT Id, Title, TriggerEvent, ProcedureSteps, ResponsiblePerson, LastReviewDate, Status FROM legacy.ContingencyPlans;"),
                            
                            ("EQAPrograms", "INSERT OR IGNORE INTO EQAPrograms (Id, Name, Provider, CycleFrequency, Status, Notes) SELECT Id, Name, Provider, CycleFrequency, Status, Notes FROM legacy.EQAPrograms;"),
                            
                            ("EQAResults", "INSERT OR IGNORE INTO EQAResults (Id, ProgramId, CycleIdentifier, ReceiptDate, ProcessingDate, SubmissionDate, Status, Score, Performance, Notes, EvidenceDocId, ReviewerUserId, ReviewDate) SELECT Id, ProgramId, CycleIdentifier, ReceiptDate, ProcessingDate, SubmissionDate, Status, Score, Performance, Notes, EvidenceDocId, ReviewerUserId, ReviewDate FROM legacy.EQAResults;"),
                            
                            ("Methods", "INSERT OR IGNORE INTO Methods (Id, Code, Name, Category, Status, CurrentVersion, EffectiveDate, DocumentId, Notes, CreatedAt, UpdatedAt, RowVersion) SELECT Id, Code, Name, Category, Status, CurrentVersion, EffectiveDate, DocumentId, Notes, CreatedAt, UpdatedAt, RowVersion FROM legacy.Methods;"),
                            
                            ("MethodVersions", "INSERT OR IGNORE INTO MethodVersions (Id, MethodId, Version, Status, ChangeDescription, DocumentPath, CreatedBy, CreatedAt, ApprovedBy, ApprovedAt) SELECT Id, MethodId, Version, Status, ChangeDescription, DocumentPath, CreatedBy, CreatedAt, ApprovedBy, ApprovedAt FROM legacy.MethodVersions;"),
                            
                            ("MethodValidations", "INSERT OR IGNORE INTO MethodValidations (Id, MethodVersionId, Parameter, Result, ExperimentCount, ReportPath, Notes) SELECT Id, MethodVersionId, Parameter, Result, ExperimentCount, ReportPath, Notes FROM legacy.MethodValidations;"),
                            
                            ("MethodAuthorizations", "INSERT OR IGNORE INTO MethodAuthorizations (Id, MethodId, UserId, UserName, AuthorizedAt, ExpiresAt, AuthorizedByUserId, AuthorizedByName) SELECT Id, MethodId, UserId, NULL, AuthorizedAt, ExpiresAt, AuthorizedByUserId, NULL FROM legacy.MethodAuthorizations;"),
                            
                            ("MethodReagents", "INSERT OR IGNORE INTO MethodReagents (Id, MethodId, ReagentId, ReagentName) SELECT Id, MethodId, ReagentId, NULL FROM legacy.MethodReagents;"),
                            
                            ("MeasurementUncertainties", "INSERT OR IGNORE INTO MeasurementUncertainties (Id, MethodId, AnalyteName, Value, Unit, CoverageFactor, ConfidenceLevel, EstimatedDate, Notes) SELECT Id, MethodId, AnalyteName, Value, Unit, CoverageFactor, ConfidenceLevel, EstimatedDate, Notes FROM legacy.MeasurementUncertainties;")
                        };

                        foreach (var migration in customMigrations)
                        {
                            try
                            {
                                await context.Database.ExecuteSqlRawAsync(migration.Sql);
                                Console.WriteLine($"✔ Migrated table {migration.Table} successfully.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"⚠ Skipped/Warning migrating table {migration.Table}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error during legacy migration process: {ex.Message}");
                    }
                    finally
                    {
                        try
                        {
                            // Always detach legacy database
                            await context.Database.ExecuteSqlRawAsync("DETACH legacy;");
                        }
                        catch { }
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(legacyDbPath))
            {
                Console.WriteLine($"⚠ Legacy migration source not found. Skipping migration: {legacyDbPath}");
            }
            // 3.5 Seed Default Training Types if empty
            if (await context.TrainingTypeCatalogs.CountAsync() == 0)
            {
                var types = new List<QMSFlowDoc.Domain.Entities.TrainingTypeCatalog>
                {
                    new() { Id = Guid.NewGuid(), Code = "CURSO", Name = "Curso", IsActive = true },
                    new() { Id = Guid.NewGuid(), Code = "TALLER", Name = "Taller / Workshop", IsActive = true },
                    new() { Id = Guid.NewGuid(), Code = "SEMINARIO", Name = "Seminario", IsActive = true },
                    new() { Id = Guid.NewGuid(), Code = "CONGRESO", Name = "Congreso / Jornada", IsActive = true },
                    new() { Id = Guid.NewGuid(), Code = "SESION_INTERNA", Name = "Sesión Interna", IsActive = true },
                    new() { Id = Guid.NewGuid(), Code = "OTRO", Name = "Otro", IsActive = true }
                };
                context.TrainingTypeCatalogs.AddRange(types);
                await context.SaveChangesAsync();
            }

            // 3.6 Seed Default Document Types if empty
            if (await context.DocumentTypes.CountAsync() == 0)
            {
                var docTypes = new List<QMSFlowDoc.Domain.Entities.DocumentType>
                {
                    new() { Id = Guid.NewGuid(), TypeCode = "PRO", Name = "Procedimiento", Description = "Procedimientos Normalizados de Trabajo (PNT)" },
                    new() { Id = Guid.NewGuid(), TypeCode = "IT", Name = "Instrucción Técnica", Description = "Instrucciones técnicas de equipos o procesos" },
                    new() { Id = Guid.NewGuid(), TypeCode = "FOR", Name = "Formulario", Description = "Registros, formularios y plantillas de trabajo" },
                    new() { Id = Guid.NewGuid(), TypeCode = "MAN", Name = "Manual", Description = "Manuales de calidad, bioseguridad, etc." },
                    new() { Id = Guid.NewGuid(), TypeCode = "GUI", Name = "Guía", Description = "Guías rápidas y material de referencia" },
                    new() { Id = Guid.NewGuid(), TypeCode = "POL", Name = "Política", Description = "Políticas y directrices de la organización" }
                };
                context.DocumentTypes.AddRange(docTypes);
                await context.SaveChangesAsync();
                Console.WriteLine("✔ Seeded default Document Types.");
            }

            // 4. Seed Default Admin User if no users exist in database (fallback)
            const string adminUser = "admin";
            const string adminEmail = "admin@qmsflowdoc.com";
            
            var existingAdmin = await userManager.FindByNameAsync(adminUser);
            if (existingAdmin == null && await context.Users.CountAsync() == 0)
            {
                var admin = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = adminUser,
                    Email = adminEmail,
                    FullName = "Administrador del Sistema",
                    EmailConfirmed = true,
                    IsActive = true
                };

                var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
                var adminPassword = config["DefaultAdminPassword"];

                if (string.IsNullOrEmpty(adminPassword))
                {
                    if (envName.Equals("Production", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("⚠ ERROR DE SEGURIDAD: La contraseña de administrador por defecto ('DefaultAdminPassword') debe estar configurada en la configuración (IConfiguration) en entornos de producción.");
                    }
                    Console.WriteLine("⚠ CONFIGURACIÓN: No se ha detectado 'DefaultAdminPassword'. El seed omitirá la creación del administrador inicial para usar el asistente interactivo en /setup.");
                }
                else
                {
                    var createAdminResult = await userManager.CreateAsync(admin, adminPassword);
                    if (createAdminResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(admin, "Administrador");
                        Console.WriteLine("✔ Administrador inicial creado a partir de la configuración.");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Error al crear el administrador inicial por seed: {string.Join(", ", createAdminResult.Errors.Select(e => e.Description))}");
                    }
                }
            }

            // 5. Seed default RolePermissions
            await SeedDefaultRolePermissionsAsync(context, roleManager);

            // 6. Seed EQA Programs
            await SeedEqaProgramsAsync(context);

            // 7. Enforce Quarantine status on all existing reagent lots (ISO 15189 compliance audit requirement)
            var existingLots = await context.ReagentLots.Where(l => l.Status != LotStatus.QUARANTINE).ToListAsync();
            if (existingLots.Any())
            {
                foreach (var lot in existingLots)
                {
                    lot.Status = LotStatus.QUARANTINE;
                    lot.ReleaseByUserId = null;
                    lot.ReleaseAt = null;
                }
                await context.SaveChangesAsync();
                Console.WriteLine($"✔ Puestos en cuarentena {existingLots.Count} lotes existentes en la base de datos.");
            }
        }

        private static async Task SeedDefaultRolePermissionsAsync(QmsDbContext context, RoleManager<ApplicationRole> roleManager)
        {
            var sections = new[] { "Documents", "Inventory", "Staff", "Quality", "Equipment", "EQA", "Audit" };
            
            var roles = await roleManager.Roles.ToListAsync();
            foreach (var role in roles)
            {
                foreach (var section in sections)
                {
                    var exists = await context.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.Section == section);
                    if (!exists)
                    {
                        var rp = new QMSFlowDoc.Domain.Identity.RolePermission
                        {
                            Id = Guid.NewGuid(),
                            RoleId = role.Id,
                            Section = section
                        };
 
                        if (role.Name == "Administrador")
                        {
                            rp.CanRead = true;
                            rp.CanCreate = true;
                            rp.CanEdit = true;
                            rp.CanDelete = true;
                            rp.CanPrint = true;
                        }
                        else if (role.Name == "Responsable calidad")
                        {
                            rp.CanRead = true;
                            rp.CanPrint = true;
                            rp.CanCreate = true;
                            rp.CanEdit = true;
                            rp.CanDelete = false;
                        }
                        else if (role.Name == "Facultativo")
                        {
                            rp.CanRead = true;
                            rp.CanPrint = true;
                            rp.CanCreate = true;
                            rp.CanEdit = true;
                            rp.CanDelete = false;
                        }
                        else if (role.Name == "Técnico")
                        {
                            rp.CanRead = true;
                            if (section == "Documents")
                            {
                                rp.CanPrint = false;
                                rp.CanCreate = false;
                                rp.CanEdit = false;
                                rp.CanDelete = false;
                            }
                            else if (section == "Inventory")
                            {
                                rp.CanPrint = true;
                                rp.CanCreate = false;
                                rp.CanEdit = true;
                                rp.CanDelete = false;
                            }
                            else if (section == "Quality")
                            {
                                rp.CanPrint = true;
                                rp.CanCreate = true;
                                rp.CanEdit = true;
                                rp.CanDelete = false;
                            }
                            else if (section == "Equipment")
                            {
                                rp.CanPrint = true;
                                rp.CanCreate = false; // Técnicos cannot register new equipment
                                rp.CanEdit = true;   // But can register status, QC, calibration, repair
                                rp.CanDelete = false;
                            }
                            else
                            {
                                rp.CanPrint = false;
                                rp.CanCreate = false;
                                rp.CanEdit = false;
                                rp.CanDelete = false;
                            }
                        }
                        else if (role.Name == "Auditor")
                        {
                            rp.CanRead = true;
                            if (section == "Documents" || section == "Quality" || section == "Equipment")
                            {
                                rp.CanPrint = true;
                                rp.CanCreate = false;
                                rp.CanEdit = false;
                                rp.CanDelete = false;
                            }
                            else if (section == "Inventory")
                            {
                                rp.CanPrint = true;
                                rp.CanCreate = true;
                                rp.CanEdit = true;
                                rp.CanDelete = false;
                            }
                            else
                            {
                                rp.CanPrint = false;
                                rp.CanCreate = false;
                                rp.CanEdit = false;
                                rp.CanDelete = false;
                            }
                        }
 
                        context.RolePermissions.Add(rp);
                    }
                }
            }
            await context.SaveChangesAsync();
        }

        private static async Task SeedEqaProgramsAsync(QmsDbContext context)
        {
            try
            {
                // Force EF to load programs to check if there are Guid formatting errors
                var allProgs = await context.EQAPrograms.ToListAsync();
            }
            catch (Exception ex) when (ex is FormatException || ex.InnerException is FormatException)
            {
                Console.WriteLine("⚠ Invalid Guid format in legacy EQA programs. Purging EQA tables to seed new clean schema...");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM EQADeviations;");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM EQASamples;");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM EQARounds;");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM EQAMappings;");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM EQAEnrollments;");
                await context.Database.ExecuteSqlRawAsync("DELETE FROM EQAPrograms;");
            }

            if (await context.EQAPrograms.AnyAsync()) return;

            var programs = new List<EQAProgram>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    InternalCode = "EQA-GECLID-LYMPH",
                    Name = "GECLID Poblaciones linfocitarias",
                    Provider = "GECLID",
                    CoordinatorEntity = "Sociedad Española de Inmunología",
                    SubArea = EQASubArea.LYMPHOCYTE_POPULATIONS,
                    SampleType = EQASampleType.Physical,
                    Periodicity = "Semestral",
                    ExpectedRoundsPerYear = 2,
                    ExpectedSamplesPerRound = 5,
                    CoveredTests = "Subpoblaciones linfocitarias T/B/NK",
                    EvaluatedParameters = "CD45+, CD3+, CD19+, NK, CD4+, CD8+",
                    TargetResult = "Concordancia con el consenso interlaboratorio > 95%",
                    GeneralAcceptanceCriteria = "Z-Score <= 2.0 respecto a la media del grupo",
                    Status = EQAStatus.ACTIVE,
                    Notes = "Programa de inmunidad celular de poblaciones linfocitarias (10 muestras anuales distribuidas en dos envíos)."
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    InternalCode = "EQA-GECLID-CD34",
                    Name = "GECLID CD34 / Stem Cells",
                    Provider = "GECLID",
                    CoordinatorEntity = "Sociedad Española de Inmunología",
                    SubArea = EQASubArea.CD34,
                    SampleType = EQASampleType.Physical,
                    Periodicity = "Semestral",
                    ExpectedRoundsPerYear = 2,
                    ExpectedSamplesPerRound = 2,
                    CoveredTests = "Cuantificación de progenitores hematopoyéticos CD34+",
                    EvaluatedParameters = "CD34+ abs/uL, CD34+ viables %, CD45+ viables %, Viabilidad %",
                    TargetResult = "Concordancia con el consenso interlaboratorio > 90%",
                    GeneralAcceptanceCriteria = "Z-Score <= 2.0",
                    Status = EQAStatus.ACTIVE,
                    Notes = "Programa de control de calidad para células madre CD34+/CD45+ viables."
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    InternalCode = "EQA-SIC-LEU",
                    Name = "SIC-SEHH Inmunofenotipaje de leucemias y linfomas",
                    Provider = "SIC-SEHH",
                    CoordinatorEntity = "Sociedad Ibérica de Citometría",
                    SubArea = EQASubArea.LEUKEMIA_LYMPHOMA,
                    SampleType = EQASampleType.FCS,
                    Periodicity = "Cuatrimestral",
                    ExpectedRoundsPerYear = 3,
                    ExpectedSamplesPerRound = 1,
                    CoveredTests = "Diagnóstico inmunofenotípico de hemopatías malignas",
                    EvaluatedParameters = "Linaje, Orientación diagnóstica, Fenotipo aberrante, Conclusión",
                    TargetResult = "Concordancia diagnóstica del 100% en linaje",
                    GeneralAcceptanceCriteria = "Consenso diagnóstico de expertos del panel",
                    Status = EQAStatus.ACTIVE,
                    Notes = "Inmunofenotipaje de leucemias y linfomas por análisis de ficheros FCS virtuales."
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    InternalCode = "EQA-SIC-LCR",
                    Name = "SIC-SEHH Subpoblaciones linfoides / LCR",
                    Provider = "SIC-SEHH",
                    CoordinatorEntity = "Sociedad Ibérica de Citometría",
                    SubArea = EQASubArea.LCR,
                    SampleType = EQASampleType.Mixed,
                    Periodicity = "Semestral",
                    ExpectedRoundsPerYear = 2,
                    ExpectedSamplesPerRound = 1,
                    CoveredTests = "Estudio de infiltración por citometría en LCR",
                    EvaluatedParameters = "Población patológica %, Fenotipo patológico, Poblaciones acompañantes",
                    TargetResult = "Identificación correcta de la presencia/ausencia de infiltración",
                    GeneralAcceptanceCriteria = "Concordancia cualitativa y cuantitativa con consenso",
                    Status = EQAStatus.ACTIVE,
                    Notes = "Estudio de subpoblaciones linfoides y detección de infiltración tumoral en líquido cefalorraquídeo."
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    InternalCode = "EQA-SIC-HPN",
                    Name = "SIC-SEHH HPN",
                    Provider = "SIC-SEHH",
                    CoordinatorEntity = "Sociedad Ibérica de Citometría",
                    SubArea = EQASubArea.HPN,
                    SampleType = EQASampleType.FCS,
                    Periodicity = "Cuatrimestral",
                    ExpectedRoundsPerYear = 3,
                    ExpectedSamplesPerRound = 1,
                    CoveredTests = "Detección y cuantificación de clones GPI-deficientes",
                    EvaluatedParameters = "Presencia/Ausencia de clon HPN, % clon en granulocitos, % clon en monocitos",
                    TargetResult = "Detección correcta del clon HPN",
                    GeneralAcceptanceCriteria = "Z-Score <= 2.0 para porcentaje cuantitativo",
                    Status = EQAStatus.ACTIVE,
                    Notes = "Detección y Diagnóstico de Hemoglobinuria Paroxística Nocturna mediante ficheros FCS virtuales."
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    InternalCode = "EQA-SIC-EMRMM",
                    Name = "SIC-SEHH EMR-MM",
                    Provider = "SIC-SEHH",
                    CoordinatorEntity = "Sociedad Ibérica de Citometría",
                    SubArea = EQASubArea.EMR_MM,
                    SampleType = EQASampleType.FCS,
                    Periodicity = "Cuatrimestral",
                    ExpectedRoundsPerYear = 3,
                    ExpectedSamplesPerRound = 1,
                    CoveredTests = "Enfermedad mínima residual en mieloma múltiple",
                    EvaluatedParameters = "Presencia/Ausencia de células plasmáticas aberrantes, % EMR, Sensibilidad",
                    TargetResult = "Cuantificación correcta de EMR en niveles ultrasensibles (< 10^-5)",
                    GeneralAcceptanceCriteria = "Detección cualitativa coherente y Z-Score <= 2.0 cuantitativo",
                    Status = EQAStatus.ACTIVE,
                    Notes = "Seguimiento de Enfermedad Mínima Residual en Mieloma Múltiple (cuatrimestral virtual)."
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    InternalCode = "EQA-SIC-EMRLLA",
                    Name = "SIC-SEHH EMR-LLA",
                    Provider = "SIC-SEHH",
                    CoordinatorEntity = "Sociedad Ibérica de Citometría",
                    SubArea = EQASubArea.EMR_LLA,
                    SampleType = EQASampleType.FCS,
                    Periodicity = "Cuatrimestral",
                    ExpectedRoundsPerYear = 3,
                    ExpectedSamplesPerRound = 1,
                    CoveredTests = "Enfermedad mínima residual en LLA",
                    EvaluatedParameters = "Presencia/Ausencia de EMR, % EMR, Fenotipo aberrante",
                    TargetResult = "Detección cualitativa exacta de la población leucémica residual",
                    GeneralAcceptanceCriteria = "Consenso del grupo de análisis de EMR-LLA",
                    Status = EQAStatus.ACTIVE,
                    Notes = "Seguimiento de Enfermedad Mínima Residual en Leucemia Linfoblástica Aguda mediante ficheros FCS."
                }
            };

            context.EQAPrograms.AddRange(programs);
            await context.SaveChangesAsync();

            // Seed initial rounds for 2026 for demonstration
            var year = 2026;
            foreach (var program in programs)
            {
                for (int r = 1; r <= program.ExpectedRoundsPerYear; r++)
                {
                    var round = new EQARound
                    {
                        Id = Guid.NewGuid(),
                        ProgramId = program.Id,
                        Year = year,
                        RoundNumber = r,
                        ExternalCode = $"{program.InternalCode}-2026-R{r}",
                        ExpectedReceiptDate = new DateTime(year, (r * 4) - 3, 15),
                        SubmissionDeadline = new DateTime(year, (r * 4) - 2, 15),
                        ExpectedReportDate = new DateTime(year, (r * 4) - 1, 15),
                        Status = EQARoundStatus.PLANNED,
                        RoundType = program.SampleType,
                        Notes = $"Ronda de evaluación número {r} planificada para el año {year}."
                    };

                    // Add dummy sample metadata
                    for (int s = 1; s <= program.ExpectedSamplesPerRound; s++)
                    {
                        round.Samples.Add(new EQASample
                        {
                            Id = Guid.NewGuid(),
                            RoundId = round.Id,
                            InternalCode = $"{round.ExternalCode}-M{s}",
                            ExternalCode = $"Muestra {s}",
                            SampleType = program.SubArea switch
                            {
                                EQASubArea.LCR => "Líquido Cefalorraquídeo (LCR)",
                                EQASubArea.EMR_MM or EQASubArea.EMR_LLA => "Médula Ósea",
                                _ => "Sangre Periférica"
                            },
                            ReceiptStatus = EQASampleStatus.CORRECT,
                            Integrity = EQAIntegrity.CORRECT
                        });
                    }

                    context.EQARounds.Add(round);
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task EnsureIsoImprovementSqliteSchemaAsync(QmsDbContext context)
        {
            if (!IsSqlite(context))
                return;

            await EnsureColumnAsync(context, "Risks", "Opportunity", "TEXT");
            await EnsureColumnAsync(context, "Risks", "ActionPlan", "TEXT");
            await EnsureColumnAsync(context, "Risks", "Responsible", "TEXT");
            await EnsureColumnAsync(context, "Risks", "DueDate", "TEXT");
            await EnsureColumnAsync(context, "Risks", "ResidualLikelihood", "INTEGER");
            await EnsureColumnAsync(context, "Risks", "ResidualImpact", "INTEGER");
            await EnsureColumnAsync(context, "Risks", "EffectivenessReview", "TEXT");
            await EnsureColumnAsync(context, "Risks", "EffectivenessReviewDate", "TEXT");
            await EnsureColumnAsync(context, "Risks", "EvidenceDocumentId", "TEXT");
            await EnsureColumnAsync(context, "Risks", "ApprovedByName", "TEXT");
            await EnsureColumnAsync(context, "Risks", "ApprovedAt", "TEXT");
            await EnsureColumnAsync(context, "Risks", "ApprovalNotes", "TEXT");

            await EnsureColumnAsync(context, "AuditPlans", "Objectives", "TEXT");
            await EnsureColumnAsync(context, "AuditPlans", "Criteria", "TEXT");
            await EnsureColumnAsync(context, "AuditPlans", "Conclusions", "TEXT");
            await EnsureColumnAsync(context, "AuditPlans", "FollowUpActions", "TEXT");
            await EnsureColumnAsync(context, "AuditPlans", "CompletedAt", "TEXT");
            await EnsureColumnAsync(context, "AuditPlans", "ProgramYear", "INTEGER NOT NULL DEFAULT 0");
            await EnsureColumnAsync(context, "AuditPlans", "InternalAuditors", "TEXT");
            await EnsureColumnAsync(context, "AuditPlans", "AuditorIndependenceStatement", "TEXT");
            await EnsureColumnAsync(context, "AuditPlans", "ApprovedByName", "TEXT");
            await EnsureColumnAsync(context, "AuditPlans", "ApprovedAt", "TEXT");
            await EnsureColumnAsync(context, "AuditPlans", "ApprovalNotes", "TEXT");

            await EnsureColumnAsync(context, "AuditFindings", "Responsible", "TEXT");
            await EnsureColumnAsync(context, "AuditFindings", "DueDate", "TEXT");
            await EnsureColumnAsync(context, "AuditFindings", "EffectivenessReview", "TEXT");
            await EnsureColumnAsync(context, "AuditFindings", "EvidenceDocumentId", "TEXT");

            await EnsureColumnAsync(context, "ManagementReviews", "PreviousActionsReview", "TEXT");
            await EnsureColumnAsync(context, "ManagementReviews", "ChangesAffectingQms", "TEXT");
            await EnsureColumnAsync(context, "ManagementReviews", "ResourceNeeds", "TEXT");
            await EnsureColumnAsync(context, "ManagementReviews", "QualityIndicatorsReview", "TEXT");
            await EnsureColumnAsync(context, "ManagementReviews", "ExternalProviderPerformance", "TEXT");
            await EnsureColumnAsync(context, "ManagementReviews", "Decisions", "TEXT");
            await EnsureColumnAsync(context, "ManagementReviews", "ImprovementOpportunities", "TEXT");
            await EnsureColumnAsync(context, "ManagementReviews", "ActionOwner", "TEXT");
            await EnsureColumnAsync(context, "ManagementReviews", "ActionDueDate", "TEXT");
            await EnsureColumnAsync(context, "ManagementReviews", "EffectivenessReview", "TEXT");
            await EnsureColumnAsync(context, "ManagementReviews", "ApprovedByName", "TEXT");
            await EnsureColumnAsync(context, "ManagementReviews", "ApprovedAt", "TEXT");
            await EnsureColumnAsync(context, "ManagementReviews", "ApprovalNotes", "TEXT");

            await EnsureColumnAsync(context, "SupplierEvaluations", "EvaluatorName", "TEXT");
            await EnsureColumnAsync(context, "SupplierEvaluations", "Criticality", "TEXT NOT NULL DEFAULT 'Media'");
            await EnsureColumnAsync(context, "SupplierEvaluations", "Scope", "TEXT NOT NULL DEFAULT ''");
            await EnsureColumnAsync(context, "SupplierEvaluations", "Decision", "TEXT NOT NULL DEFAULT 'Aprobado'");
            await EnsureColumnAsync(context, "SupplierEvaluations", "CorrectiveActions", "TEXT");
            await EnsureColumnAsync(context, "SupplierEvaluations", "NextEvaluationDate", "TEXT");

            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS QualityIndicators (
                    Id TEXT NOT NULL CONSTRAINT PK_QualityIndicators PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Area TEXT NOT NULL,
                    Period TEXT NOT NULL,
                    TargetValue TEXT NOT NULL,
                    TargetRule TEXT NOT NULL,
                    ActualValue TEXT NOT NULL,
                    Unit TEXT NOT NULL,
                    Trend TEXT NOT NULL,
                    MeetsTarget INTEGER NOT NULL,
                    Analysis TEXT NULL,
                    ActionPlan TEXT NULL,
                    Responsible TEXT NULL,
                    DueDate TEXT NULL,
                    EffectivenessReview TEXT NULL,
                    EffectivenessReviewDate TEXT NULL,
                    EvidenceDocumentId TEXT NULL,
                    ApprovedByName TEXT NULL,
                    ApprovedAt TEXT NULL,
                    ApprovalNotes TEXT NULL,
                    CreatedAt TEXT NOT NULL
                );
                """);
            await EnsureColumnAsync(context, "QualityIndicators", "EffectivenessReview", "TEXT");
            await EnsureColumnAsync(context, "QualityIndicators", "EffectivenessReviewDate", "TEXT");
            await EnsureColumnAsync(context, "QualityIndicators", "EvidenceDocumentId", "TEXT");
            await EnsureColumnAsync(context, "QualityIndicators", "ApprovedByName", "TEXT");
            await EnsureColumnAsync(context, "QualityIndicators", "ApprovedAt", "TEXT");
            await EnsureColumnAsync(context, "QualityIndicators", "ApprovalNotes", "TEXT");
            await context.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_QualityIndicators_Name_Period ON QualityIndicators (Name, Period);");
            await context.Database.ExecuteSqlRawAsync("UPDATE AuditPlans SET ProgramYear = CAST(strftime('%Y', ScheduledDate) AS INTEGER) WHERE ProgramYear = 0 AND ScheduledDate IS NOT NULL;");
        }

        private static async Task EnsureColumnAsync(QmsDbContext context, string table, string column, string definition)
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info({QuoteSqliteIdentifier(table)});";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }

            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE {QuoteSqliteIdentifier(table)} ADD COLUMN {QuoteSqliteIdentifier(column)} {definition};";
            await alterCommand.ExecuteNonQueryAsync();
        }

        private static bool IsSqlite(QmsDbContext context) =>
            string.Equals(context.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.OrdinalIgnoreCase);

        private static async Task<bool> IsMigrationAppliedAsync(QmsDbContext context, string migrationId)
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            if (!await TableExistsAsync(connection, "__EFMigrationsHistory"))
                return false;

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(1)
                FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = @migrationId;
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@migrationId";
            parameter.Value = migrationId;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        private static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection connection, string tableName)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(1)
                FROM sqlite_master
                WHERE type = 'table' AND name = @tableName;
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tableName";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        private static async Task MarkMigrationAppliedAsync(QmsDbContext context, string migrationId, string productVersion)
        {
            if (await IsMigrationAppliedAsync(context, migrationId))
                return;

            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES (@migrationId, @productVersion);
                """;

            var migrationParameter = command.CreateParameter();
            migrationParameter.ParameterName = "@migrationId";
            migrationParameter.Value = migrationId;
            command.Parameters.Add(migrationParameter);

            var versionParameter = command.CreateParameter();
            versionParameter.ParameterName = "@productVersion";
            versionParameter.Value = productVersion;
            command.Parameters.Add(versionParameter);

            await command.ExecuteNonQueryAsync();
        }

        private static string QuoteSqliteIdentifier(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
