using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Data;

/// <summary>
/// Central EF Core DbContext for QMSFlowDoc V2.
/// Maps all domain models to SQL Server Express tables.
/// Uses optimistic concurrency via RowVersion on key entities.
/// </summary>
public class QmsFlowDocDbContext : DbContext
{
    public QmsFlowDocDbContext(DbContextOptions<QmsFlowDocDbContext> options)
        : base(options) { }

    // ── Identity & Authorization ──
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();

    // ── Documents ──
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<Folder> Folders => Set<Folder>();

    // ── Equipment ──
    public DbSet<Equipment> Equipments => Set<Equipment>();
    public DbSet<MaintenancePlan> MaintenancePlans => Set<MaintenancePlan>();
    public DbSet<MaintenanceEvent> MaintenanceEvents => Set<MaintenanceEvent>();
    public DbSet<EquipmentHistory> EquipmentHistory => Set<EquipmentHistory>();
    public DbSet<EquipmentFunctionalQC> EquipmentFunctionalQC => Set<EquipmentFunctionalQC>();

    // ── Inventory & Suppliers ──
    public DbSet<Reagent> Reagents => Set<Reagent>();
    public DbSet<ReagentLot> ReagentLots => Set<ReagentLot>();
    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<ReagentType> ReagentTypes => Set<ReagentType>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierEvaluation> SupplierEvaluations => Set<SupplierEvaluation>();

    // ── Staff & Competencies ──
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<StaffTraining> StaffTrainings => Set<StaffTraining>();
    public DbSet<TrainingActivity> TrainingActivities => Set<TrainingActivity>();
    public DbSet<TrainingTypeCatalog> TrainingTypeCatalogs => Set<TrainingTypeCatalog>();
    public DbSet<CompetencyCatalog> CompetencyCatalogs => Set<CompetencyCatalog>();
    public DbSet<CompetencyEvalTemplate> CompetencyEvalTemplates => Set<CompetencyEvalTemplate>();
    public DbSet<CompetencyAssessmentMethod> CompetencyAssessmentMethods => Set<CompetencyAssessmentMethod>();
    public DbSet<CompetencyEvaluation> CompetencyEvaluations => Set<CompetencyEvaluation>();
    public DbSet<StaffCompetencyStatus> StaffCompetencyStatuses => Set<StaffCompetencyStatus>();
    public DbSet<Competency> CompetenciesLegacy => Set<Competency>();

    // ── Authorizations ──
    public DbSet<AuthorizationCatalog> AuthorizationCatalogs => Set<AuthorizationCatalog>();
    public DbSet<AuthorizationRequiredCompetency> AuthorizationRequiredCompetencies => Set<AuthorizationRequiredCompetency>();
    public DbSet<StaffAuthorization> StaffAuthorizations => Set<StaffAuthorization>();

    // ── Quality ──
    public DbSet<Nonconformity> Nonconformities => Set<Nonconformity>();
    public DbSet<CapaAction> CapaActions => Set<CapaAction>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<ComplaintAction> ComplaintActions => Set<ComplaintAction>();

    // ── Improvement ──
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<AuditPlan> AuditPlans => Set<AuditPlan>();
    public DbSet<AuditFinding> AuditFindings => Set<AuditFinding>();
    public DbSet<ManagementReview> ManagementReviews => Set<ManagementReview>();
    public DbSet<QualityIndicator> QualityIndicators => Set<QualityIndicator>();
    public DbSet<IQCResult> IQCResults => Set<IQCResult>();
    public DbSet<ContingencyPlan> ContingencyPlans => Set<ContingencyPlan>();

    // ── EQA ──
    public DbSet<EQAProgram> EQAPrograms => Set<EQAProgram>();
    public DbSet<EQAResult> EQAResults => Set<EQAResult>();

    // ── Methods ──
    public DbSet<Method> Methods => Set<Method>();
    public DbSet<MethodAuthorization> MethodAuthorizations => Set<MethodAuthorization>();
    public DbSet<MethodReagent> MethodReagents => Set<MethodReagent>();
    public DbSet<MeasurementUncertainty> MeasurementUncertainties => Set<MeasurementUncertainty>();
    public DbSet<MethodVersion> MethodVersions => Set<MethodVersion>();
    public DbSet<MethodValidation> MethodValidations => Set<MethodValidation>();

    // ── Audit & Config ──
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ═══════════════════════════════════════════
        // Users, Roles, Permissions
        // ═══════════════════════════════════════════
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).HasMaxLength(100).IsRequired();
            e.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            e.Property(u => u.Email).HasMaxLength(256);
            e.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(u => u.RowVersion).IsRowVersion();

            e.HasMany(u => u.Roles)
             .WithMany()
             .UsingEntity<Dictionary<string, object>>(
                 "UserRoles",
                 j => j.HasOne<Role>().WithMany().HasForeignKey("RoleId"),
                 j => j.HasOne<User>().WithMany().HasForeignKey("UserId"));
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.RoleName).IsUnique();
            e.Property(r => r.RoleName).HasMaxLength(100).IsRequired();

            e.HasMany(r => r.Permissions)
             .WithMany()
             .UsingEntity("RolePermissions");
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.ToTable("Permissions");
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.PermissionKey).IsUnique();
            e.Property(p => p.PermissionKey).HasMaxLength(150).IsRequired();
        });

        // ═══════════════════════════════════════════
        // Documents
        // ═══════════════════════════════════════════
        modelBuilder.Entity<Document>(e =>
        {
            e.ToTable("Documents");
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.DocCode).IsUnique();
            e.Property(d => d.DocCode).HasMaxLength(50).IsRequired();
            e.Property(d => d.Title).HasMaxLength(500).IsRequired();
            e.Property(d => d.Area).HasMaxLength(200);
            e.Property(d => d.Process).HasMaxLength(200);
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(d => d.RowVersion).IsRowVersion();
            e.HasQueryFilter(d => !d.IsDeleted);

            e.HasOne(d => d.DocumentType).WithMany().HasForeignKey(d => d.DocumentTypeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(d => d.Folder).WithMany(f => f.Documents).HasForeignKey(d => d.FolderId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(d => d.Versions).WithOne().HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentVersion>(e =>
        {
            e.ToTable("DocumentVersions");
            e.HasKey(v => v.Id);
            e.Property(v => v.VersionLabel).HasMaxLength(50);
            e.Property(v => v.ChangeSummary).HasMaxLength(2000);
            e.Property(v => v.MimeType).HasMaxLength(200);
            e.Property(v => v.FileName).HasMaxLength(500);
            e.Property(v => v.Sha256).HasMaxLength(64);
            e.Property(v => v.RowVersion).IsRowVersion();
            // RelativePath replaces CloudFileId + LocalFilePath
            e.Property(v => v.LocalFilePath).HasColumnName("RelativePath").HasMaxLength(1000);
            e.Ignore(v => v.CloudFileId);
            e.Ignore(v => v.CloudEtag);
        });

        modelBuilder.Entity<DocumentType>(e =>
        {
            e.ToTable("DocumentTypes");
            e.HasKey(t => t.Id);
            e.HasIndex(t => t.TypeCode).IsUnique();
            e.Property(t => t.TypeCode).HasMaxLength(30).IsRequired();
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Folder>(e =>
        {
            e.ToTable("Folders");
            e.HasKey(f => f.Id);
            e.Property(f => f.Name).HasMaxLength(200).IsRequired();
            e.HasMany(f => f.SubFolders).WithOne().HasForeignKey(f => f.ParentFolderId).OnDelete(DeleteBehavior.Restrict);
        });

        // ═══════════════════════════════════════════
        // Equipment
        // ═══════════════════════════════════════════
        modelBuilder.Entity<Equipment>(e =>
        {
            e.ToTable("Equipments");
            e.HasKey(eq => eq.Id);
            e.HasIndex(eq => eq.InternalId).IsUnique().HasFilter("[InternalId] IS NOT NULL");
            e.Property(eq => eq.Name).HasMaxLength(300).IsRequired();
            e.Property(eq => eq.Manufacturer).HasMaxLength(200);
            e.Property(eq => eq.Model).HasMaxLength(200);
            e.Property(eq => eq.SerialNumber).HasMaxLength(200);
            e.Property(eq => eq.InternalId).HasMaxLength(50);
            e.Property(eq => eq.AssetTag).HasMaxLength(50);
            e.Property(eq => eq.Location).HasMaxLength(300);
            e.Property(eq => eq.Status).HasConversion<int>();
            e.Property(eq => eq.RowVersion).IsRowVersion();
            e.HasQueryFilter(eq => !eq.IsDeleted);

            e.HasMany(eq => eq.MaintenancePlans).WithOne().HasForeignKey(p => p.EquipmentId);
            e.HasMany(eq => eq.MaintenanceEvents).WithOne().HasForeignKey(ev => ev.EquipmentId);
            e.HasMany(eq => eq.FunctionalQCs).WithOne(qc => qc.Equipment!).HasForeignKey(qc => qc.EquipmentId);
        });

        modelBuilder.Entity<MaintenancePlan>(e =>
        {
            e.ToTable("MaintenancePlans");
            e.HasKey(p => p.Id);
            e.Property(p => p.PlanName).HasMaxLength(300);
        });

        modelBuilder.Entity<MaintenanceEvent>(e =>
        {
            e.ToTable("MaintenanceEvents");
            e.HasKey(ev => ev.Id);
            e.Property(ev => ev.EventType).HasConversion<int>();
            e.Property(ev => ev.Outcome).HasMaxLength(100);
            e.Property(ev => ev.CertificatePath).HasMaxLength(1000);
            e.Property(ev => ev.Cost).HasColumnType("decimal(18,2)");
            e.Property(ev => ev.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<EquipmentHistory>(e =>
        {
            e.ToTable("EquipmentHistory");
            e.HasKey(h => h.Id);
            e.Property(h => h.ActionType).HasMaxLength(50);
            e.Property(h => h.UserName).HasMaxLength(200);
            e.HasIndex(h => h.EquipmentId);
        });

        modelBuilder.Entity<EquipmentFunctionalQC>(e =>
        {
            e.ToTable("EquipmentDailyQC"); // Map to same table to keep local DB compatibility
            e.HasKey(qc => qc.Id);
            e.Property(qc => qc.LotNumber).HasMaxLength(100);
            e.HasIndex(qc => new { qc.EquipmentId, qc.PerformedAt });
        });

        // ═══════════════════════════════════════════
        // Inventory & Suppliers
        // ═══════════════════════════════════════════
        modelBuilder.Entity<Reagent>(e =>
        {
            e.ToTable("Reagents");
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).HasMaxLength(300).IsRequired();
            e.Property(r => r.Manufacturer).HasMaxLength(200);
            e.Property(r => r.Reference).HasMaxLength(200);
            e.Property(r => r.ReagentType).HasMaxLength(100);
            e.Property(r => r.MinStock).HasColumnType("decimal(18,2)");
            e.Property(r => r.TargetStock).HasColumnType("decimal(18,2)");
            e.Property(r => r.ReorderQty).HasColumnType("decimal(18,2)");
            e.Property(r => r.RowVersion).IsRowVersion();
            e.HasQueryFilter(r => !r.IsDeleted);

            e.HasOne(r => r.Supplier).WithMany().HasForeignKey(r => r.SupplierId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.DefaultLocation).WithMany().HasForeignKey(r => r.DefaultLocationId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(r => r.Lots).WithOne().HasForeignKey(l => l.ReagentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReagentLot>(e =>
        {
            e.ToTable("ReagentLots");
            e.HasKey(l => l.Id);
            e.Property(l => l.LotNumber).HasMaxLength(100).IsRequired();
            e.Property(l => l.ReceivedQty).HasColumnType("decimal(18,2)");
            e.Property(l => l.AvailableQty).HasColumnType("decimal(18,2)");
            e.Property(l => l.Status).HasConversion<int>();
            e.Property(l => l.RowVersion).IsRowVersion();
            e.HasOne(l => l.Location).WithMany().HasForeignKey(l => l.LocationId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StorageLocation>(e =>
        {
            e.ToTable("StorageLocations");
            e.HasKey(l => l.Id);
            e.Property(l => l.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<InventoryMovement>(e =>
        {
            e.ToTable("InventoryMovements");
            e.HasKey(m => m.Id);
            e.Property(m => m.Qty).HasColumnType("decimal(18,2)");
            e.Property(m => m.MovementType).HasConversion<int>();
            e.Property(m => m.Reason).HasMaxLength(500);
            e.HasOne(m => m.Reagent).WithMany().HasForeignKey(m => m.ReagentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.ReagentLot).WithMany().HasForeignKey(m => m.ReagentLotId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ReagentType>(e =>
        {
            e.ToTable("ReagentTypes");
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Supplier>(e =>
        {
            e.ToTable("Suppliers");
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(300).IsRequired();
            e.Property(s => s.Email).HasMaxLength(256);
            e.Property(s => s.Phone).HasMaxLength(50);
            e.Property(s => s.Type).HasConversion<int>();
            e.Property(s => s.QualityStatus).HasConversion<int>();
            e.Property(s => s.RowVersion).IsRowVersion();
            e.HasQueryFilter(s => !s.IsDeleted);
        });

        modelBuilder.Entity<SupplierEvaluation>(e =>
        {
            e.ToTable("SupplierEvaluations");
            e.HasKey(se => se.Id);
            e.Property(se => se.EvaluatedPeriod).HasMaxLength(50);
            e.Property(se => se.EvaluatorName).HasMaxLength(200);
            e.Property(se => se.Criticality).HasMaxLength(50);
            e.Property(se => se.Scope).HasMaxLength(500);
            e.Property(se => se.Decision).HasMaxLength(100);
            e.Property(se => se.AttachmentPath).HasMaxLength(1000);
            e.HasOne(se => se.Supplier).WithMany().HasForeignKey(se => se.SupplierId).OnDelete(DeleteBehavior.Cascade);
            e.Ignore(se => se.AverageScore); // Calculated property
        });

        // ═══════════════════════════════════════════
        // Staff & Training
        // ═══════════════════════════════════════════
        modelBuilder.Entity<StaffProfile>(e =>
        {
            e.ToTable("StaffProfiles");
            e.HasKey(s => s.Id);
            e.Property(s => s.PositionTitle).HasMaxLength(200);
            e.Property(s => s.Department).HasMaxLength(200);
            e.Property(s => s.RowVersion).IsRowVersion();

            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(s => s.Trainings).WithOne(t => t.Staff!).HasForeignKey(t => t.StaffId);
            e.HasMany(s => s.CompetencyEvaluations).WithOne(c => c.Staff!).HasForeignKey(c => c.StaffId);
            e.HasMany(s => s.CompetencyStatuses).WithOne(cs => cs.Staff!).HasForeignKey(cs => cs.StaffId);
            e.HasMany(s => s.Authorizations).WithOne(a => a.Staff!).HasForeignKey(a => a.StaffId);
        });

        modelBuilder.Entity<TrainingTypeCatalog>(e =>
        {
            e.ToTable("TrainingTypeCatalogs");
            e.HasKey(t => t.Id);
            e.Property(t => t.Code).HasMaxLength(50);
            e.Property(t => t.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<TrainingActivity>(e =>
        {
            e.ToTable("TrainingActivities");
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).HasMaxLength(500);
            e.Property(a => a.Provider).HasMaxLength(300);
            e.Property(a => a.Modality).HasMaxLength(50);
            e.Property(a => a.Hours).HasColumnType("decimal(8,2)");
            e.Property(a => a.Status).HasMaxLength(30);

            e.HasOne(a => a.TrainingType).WithMany().HasForeignKey(a => a.TrainingTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(a => a.Assignments).WithOne(s => s.TrainingActivity!).HasForeignKey(s => s.TrainingActivityId);
        });

        modelBuilder.Entity<StaffTraining>(e =>
        {
            e.ToTable("StaffTrainings");
            e.HasKey(st => st.Id);
            e.Property(st => st.ParticipationRole).HasMaxLength(50);
            e.Property(st => st.Result).HasMaxLength(50);
            e.Property(st => st.Status).HasMaxLength(30);
        });

        // ═══════════════════════════════════════════
        // Competency
        // ═══════════════════════════════════════════
        modelBuilder.Entity<CompetencyCatalog>(e =>
        {
            e.ToTable("CompetencyCatalogs");
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Code).IsUnique();
            e.Property(c => c.Code).HasMaxLength(50);
            e.Property(c => c.Name).HasMaxLength(300);
            e.Property(c => c.RoleScope).HasMaxLength(50);
            e.Property(c => c.Area).HasMaxLength(100);
        });

        modelBuilder.Entity<CompetencyEvalTemplate>(e =>
        {
            e.ToTable("CompetencyEvalTemplates");
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(300);
            e.HasOne(t => t.Competency).WithMany().HasForeignKey(t => t.CompetencyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompetencyAssessmentMethod>(e =>
        {
            e.ToTable("CompetencyAssessmentMethods");
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.Code).IsUnique();
            e.Property(m => m.Code).HasMaxLength(50);
            e.Property(m => m.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<CompetencyEvaluation>(e =>
        {
            e.ToTable("CompetencyEvaluations");
            e.HasKey(ev => ev.Id);
            e.Property(ev => ev.Outcome).HasMaxLength(50);
            e.Property(ev => ev.Status).HasMaxLength(30);

            e.HasOne(ev => ev.Competency).WithMany().HasForeignKey(ev => ev.CompetencyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ev => ev.Template).WithMany().HasForeignKey(ev => ev.TemplateId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(ev => ev.MethodsUsed).WithMany();
        });

        modelBuilder.Entity<StaffCompetencyStatus>(e =>
        {
            e.ToTable("StaffCompetencyStatuses");
            e.HasKey(cs => new { cs.StaffId, cs.CompetencyId }); // Composite PK
            e.Property(cs => cs.CurrentStatus).HasMaxLength(50);
            e.HasOne(cs => cs.Competency).WithMany().HasForeignKey(cs => cs.CompetencyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(cs => cs.LastEvaluation).WithMany().HasForeignKey(cs => cs.LastEvaluationId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Competency>(e =>
        {
            e.ToTable("Competencies");
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Code).IsUnique();
            e.Property(c => c.Code).HasMaxLength(50);
            e.Property(c => c.Name).HasMaxLength(300);
        });

        // ═══════════════════════════════════════════
        // Authorizations
        // ═══════════════════════════════════════════
        modelBuilder.Entity<AuthorizationCatalog>(e =>
        {
            e.ToTable("AuthorizationCatalogs");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.Code).IsUnique();
            e.Property(a => a.Code).HasMaxLength(50);
            e.Property(a => a.Name).HasMaxLength(300);
            e.Property(a => a.RoleScope).HasMaxLength(50);
            e.HasMany(a => a.RequiredCompetencies).WithOne(rc => rc.Authorization!).HasForeignKey(rc => rc.AuthorizationId);
        });

        modelBuilder.Entity<AuthorizationRequiredCompetency>(e =>
        {
            e.ToTable("AuthorizationRequiredCompetencies");
            e.HasKey(rc => new { rc.AuthorizationId, rc.CompetencyId });
            e.HasOne(rc => rc.Competency).WithMany().HasForeignKey(rc => rc.CompetencyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StaffAuthorization>(e =>
        {
            e.ToTable("StaffAuthorizations");
            e.HasKey(sa => sa.Id);
            e.Property(sa => sa.Status).HasMaxLength(30);
            e.Property(sa => sa.RowVersion).IsRowVersion();
            e.HasOne(sa => sa.Authorization).WithMany().HasForeignKey(sa => sa.AuthorizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(sa => sa.GrantedByUser).WithMany().HasForeignKey(sa => sa.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ═══════════════════════════════════════════
        // Quality (NC, CAPA, Complaints)
        // ═══════════════════════════════════════════
        modelBuilder.Entity<Nonconformity>(e =>
        {
            e.ToTable("Nonconformities");
            e.HasKey(nc => nc.Id);
            e.Property(nc => nc.Title).HasMaxLength(500).IsRequired();
            e.Property(nc => nc.Severity).HasConversion<int>();
            e.Property(nc => nc.Status).HasConversion<int>();
            e.Property(nc => nc.Origin).HasMaxLength(100);
            e.Property(nc => nc.RowVersion).IsRowVersion();
            e.HasMany(nc => nc.Actions).WithOne(a => a.Nonconformity!).HasForeignKey(a => a.NCId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CapaAction>(e =>
        {
            e.ToTable("CapaActions");
            e.HasKey(a => a.Id);
            e.Property(a => a.ActionType).HasConversion<int>();
            e.Property(a => a.Status).HasConversion<int>();
        });

        modelBuilder.Entity<Complaint>(e =>
        {
            e.ToTable("Complaints");
            e.HasKey(c => c.Id);
            e.Property(c => c.Source).HasMaxLength(300);
            e.Property(c => c.Category).HasConversion<int>();
            e.Property(c => c.ClaimantType).HasConversion<int>();
            e.Property(c => c.ClinicalImpact).HasConversion<int>();
            e.Property(c => c.Status).HasConversion<int>();
            e.Property(c => c.RowVersion).IsRowVersion();
            e.HasMany(c => c.Actions).WithOne().HasForeignKey(a => a.ComplaintId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ComplaintAction>(e =>
        {
            e.ToTable("ComplaintActions");
            e.HasKey(a => a.Id);
            e.Property(a => a.ActionType).HasConversion<int>();
            e.Property(a => a.Status).HasConversion<int>();
        });

        // ═══════════════════════════════════════════
        // Improvement (Risks, Audits, IQC, Contingency)
        // ═══════════════════════════════════════════
        modelBuilder.Entity<Risk>(e =>
        {
            e.ToTable("Risks");
            e.HasKey(r => r.Id);
            e.Property(r => r.Title).HasMaxLength(500);
            e.Property(r => r.Category).HasMaxLength(100);
            e.Property(r => r.Likelihood).HasConversion<int>();
            e.Property(r => r.Impact).HasConversion<int>();
            e.Property(r => r.ResidualLikelihood).HasConversion<int?>();
            e.Property(r => r.ResidualImpact).HasConversion<int?>();
            e.Property(r => r.Status).HasConversion<int>();
            e.Property(r => r.RowVersion).IsRowVersion();
            e.Ignore(r => r.RiskScore); // Calculated property
            e.Ignore(r => r.ResidualRiskScore);
            e.HasOne(r => r.Owner).WithMany().HasForeignKey(r => r.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditPlan>(e =>
        {
            e.ToTable("AuditPlans");
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).HasMaxLength(500);
            e.Property(a => a.Status).HasConversion<int>();
            e.Property(a => a.RowVersion).IsRowVersion();
            e.HasOne(a => a.ReportDocument).WithMany().HasForeignKey(a => a.ReportDocumentId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(a => a.Findings).WithOne().HasForeignKey(f => f.AuditPlanId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditFinding>(e =>
        {
            e.ToTable("AuditFindings");
            e.HasKey(f => f.Id);
            e.Property(f => f.Type).HasConversion<int>();
            e.Property(f => f.IsoRequirement).HasMaxLength(100);
            e.HasOne(f => f.RelatedNC).WithMany().HasForeignKey(f => f.RelatedNCId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ManagementReview>(e =>
        {
            e.ToTable("ManagementReviews");
            e.HasKey(r => r.Id);
            e.HasOne(r => r.MinutesDocument).WithMany().HasForeignKey(r => r.MinutesDocumentId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QualityIndicator>(e =>
        {
            e.ToTable("QualityIndicators");
            e.HasKey(i => i.Id);
            e.Property(i => i.Name).HasMaxLength(300);
            e.Property(i => i.Area).HasMaxLength(150);
            e.Property(i => i.Period).HasMaxLength(50);
            e.Property(i => i.Unit).HasMaxLength(50);
            e.Property(i => i.Trend).HasMaxLength(50);
            e.Property(i => i.TargetRule).HasMaxLength(20);
            e.Property(i => i.TargetValue).HasPrecision(18, 2);
            e.Property(i => i.ActualValue).HasPrecision(18, 2);
            e.HasIndex(i => new { i.Name, i.Period });
        });

        modelBuilder.Entity<IQCResult>(e =>
        {
            e.ToTable("IQCResults");
            e.HasKey(r => r.Id);
            e.Property(r => r.EquipmentName).HasMaxLength(300);
            e.Property(r => r.AnalyteName).HasMaxLength(300);
            e.Property(r => r.Level).HasMaxLength(100);
            e.Property(r => r.Status).HasConversion<int>();
            e.Property(r => r.WestgardRule).HasMaxLength(30);
            e.HasIndex(r => new { r.EquipmentName, r.AnalyteName, r.Date });
        });

        modelBuilder.Entity<ContingencyPlan>(e =>
        {
            e.ToTable("ContingencyPlans");
            e.HasKey(p => p.Id);
            e.Property(p => p.Title).HasMaxLength(500);
            e.Property(p => p.Status).HasConversion<int>();
        });

        // ═══════════════════════════════════════════
        // EQA
        // ═══════════════════════════════════════════
        modelBuilder.Entity<EQAProgram>(e =>
        {
            e.ToTable("EQAPrograms");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(300);
            e.Property(p => p.Provider).HasMaxLength(300);
            e.Property(p => p.Status).HasConversion<int>();
            e.HasMany(p => p.Results).WithOne().HasForeignKey(r => r.ProgramId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EQAResult>(e =>
        {
            e.ToTable("EQAResults");
            e.HasKey(r => r.Id);
            e.Property(r => r.CycleIdentifier).HasMaxLength(50);
            e.Property(r => r.Status).HasConversion<int>();
            e.Property(r => r.Performance).HasConversion<int>();
            e.Property(r => r.Score).HasColumnType("decimal(10,2)");
        });

        // ═══════════════════════════════════════════
        // Methods
        // ═══════════════════════════════════════════
        modelBuilder.Entity<Method>(e =>
        {
            e.ToTable("Methods");
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.Code).IsUnique();
            e.Property(m => m.Code).HasMaxLength(50);
            e.Property(m => m.Name).HasMaxLength(300);
            e.Property(m => m.Status).HasConversion<int>();
            e.Property(m => m.RowVersion).IsRowVersion();
            e.HasMany(m => m.Authorizations).WithOne().HasForeignKey(a => a.MethodId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MethodVersion>(e =>
        {
            e.ToTable("MethodVersions");
            e.HasKey(v => v.Id);
        });

        modelBuilder.Entity<MethodValidation>(e =>
        {
            e.ToTable("MethodValidations");
            e.HasKey(v => v.Id);
        });

        modelBuilder.Entity<MethodAuthorization>(e =>
        {
            e.ToTable("MethodAuthorizations");
            e.HasKey(a => a.Id);
            e.Property(a => a.UserName).HasMaxLength(200);
        });

        modelBuilder.Entity<MethodReagent>(e =>
        {
            e.ToTable("MethodReagents");
            e.HasKey(r => r.Id);
            e.Property(r => r.ReagentName).HasMaxLength(300);
        });

        modelBuilder.Entity<MeasurementUncertainty>(e =>
        {
            e.ToTable("MeasurementUncertainties");
            e.HasKey(m => m.Id);
            e.Property(m => m.AnalyteName).HasMaxLength(300);
            e.Property(m => m.Unit).HasMaxLength(50);
            e.Property(m => m.ConfidenceLevel).HasMaxLength(30);
        });

        // ═══════════════════════════════════════════
        // Audit & System
        // ═══════════════════════════════════════════
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(a => a.Id);
            e.Property(a => a.Action).HasMaxLength(50).IsRequired();
            e.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
            e.Property(a => a.UserName).HasMaxLength(200);
            e.Property(a => a.MachineName).HasMaxLength(100);
            e.Property(a => a.Result).HasMaxLength(30);
            e.Property(a => a.IntegrityHash).HasMaxLength(64);
            e.HasIndex(a => a.Timestamp);
            e.HasIndex(a => a.EntityType);
        });

        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.ToTable("SystemSettings");
            e.HasKey(s => s.Key);
            e.Property(s => s.Key).HasMaxLength(200);
            e.Property(s => s.Value).HasMaxLength(4000);
        });

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var rowVersionProp = entityType.FindProperty("RowVersion");
                if (rowVersionProp != null)
                {
                    rowVersionProp.IsConcurrencyToken = false;
                    rowVersionProp.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                }

                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(Guid))
                    {
                        property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.GuidToStringConverter());
                    }
                    else if (property.ClrType == typeof(Guid?))
                    {
                        property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.GuidToStringConverter());
                    }
                }
            }
        }
    }
}
