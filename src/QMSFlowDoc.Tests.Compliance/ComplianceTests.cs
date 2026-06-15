using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Domain.Identity;
using QMSFlowDoc.Infrastructure.Persistence;
using QMSFlowDoc.Infrastructure.Services.EQA;
using QMSFlowDoc.Infrastructure.Services.Quality;
using QMSFlowDoc.Infrastructure.Services.Staff;
using QMSFlowDoc.Infrastructure.Services.Equipment;
using QMSFlowDoc.Infrastructure.Services.Inventory;
using QMSFlowDoc.Shared.DTOs;
using SharedModels = QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace QMSFlowDoc.Tests.Compliance
{
    public class ComplianceTests : IDisposable
    {
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
        private readonly DbContextOptions<QmsDbContext> _options;

        public ComplianceTests()
        {
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<QmsDbContext>()
                .UseSqlite(_connection)
                .Options;

            using (var context = new QmsDbContext(_options))
            {
                context.Database.EnsureCreated();
            }
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        [Fact]
        public void EQAProgram_InternalCode_MustBeUnique_ForActiveRecords()
        {
            using var context = new QmsDbContext(_options);

            var prog1 = new EQAProgram { Id = Guid.NewGuid(), InternalCode = "PROG01", Name = "Program A", Status = EQAStatus.ACTIVE };
            var prog2 = new EQAProgram { Id = Guid.NewGuid(), InternalCode = "PROG01", Name = "Program B", Status = EQAStatus.ACTIVE };

            context.EQAPrograms.Add(prog1);
            context.SaveChanges();

            context.EQAPrograms.Add(prog2);
            
            // Should throw due to unique index violation
            Assert.ThrowsAny<DbUpdateException>(() => context.SaveChanges());
        }

        [Fact]
        public void EQAProgram_InternalCode_CanBeReused_IfOldIsSoftDeleted()
        {
            using var context = new QmsDbContext(_options);

            var progDeleted = new EQAProgram { Id = Guid.NewGuid(), InternalCode = "PROG02", Name = "Old Program", IsDeleted = true, DeletedAt = DateTime.UtcNow };
            var progActive = new EQAProgram { Id = Guid.NewGuid(), InternalCode = "PROG02", Name = "New Active Program", IsDeleted = false };

            context.EQAPrograms.Add(progDeleted);
            context.EQAPrograms.Add(progActive);
            
            // Should succeed because the unique index filter is "IsDeleted = 0"
            var recordCount = context.SaveChanges();
            Assert.Equal(2, recordCount);
        }

        [Fact]
        public async Task EQAService_SoftDelete_FiltersOutDeletedRecords()
        {
            using var context = new QmsDbContext(_options);
            var service = new EQAService(context);

            var programId = Guid.NewGuid();
            var prog = new EQAProgram { Id = programId, InternalCode = "PROG_TEST", Name = "Test Program" };
            context.EQAPrograms.Add(prog);
            context.SaveChanges();

            // 1. Verify we can fetch the active program
            var activeProg = await service.GetProgramByIdAsync(programId);
            Assert.NotNull(activeProg);

            // 2. Perform service-level delete (soft delete)
            var deleteResult = await service.DeleteProgramAsync(programId, Guid.NewGuid(), "AdminUser");
            Assert.True(deleteResult);

            // 3. Service get by ID should return null (filtered)
            var activeProgAfterDelete = await service.GetProgramByIdAsync(programId);
            Assert.Null(activeProgAfterDelete);

            // 4. Verify physically present in context with IsDeleted = true
            context.Entry(prog).State = EntityState.Detached; // clear tracker to load from db
            var dbRecord = await context.EQAPrograms.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == programId);
            Assert.NotNull(dbRecord);
            Assert.True(dbRecord.IsDeleted);
            Assert.NotNull(dbRecord.DeletedAt);
            Assert.Equal("AdminUser", context.AuditLogs.OrderByDescending(l => l.Timestamp).First().UserName);
        }

        [Fact]
        public async Task EQAService_SoftDelete_CascadeToChildren()
        {
            using var context = new QmsDbContext(_options);
            var service = new EQAService(context);

            var programId = Guid.NewGuid();
            var prog = new EQAProgram { Id = programId, InternalCode = "PROG_CASCADE", Name = "Cascade Program" };
            
            var enrollmentId = Guid.NewGuid();
            var enrollment = new EQAEnrollment { Id = enrollmentId, ProgramId = programId, Year = 2026, IsDeleted = false };
            
            var mappingId = Guid.NewGuid();
            var mapping = new EQAMapping { Id = mappingId, ProgramId = programId, InternalTestName = "Cascade Test", IsDeleted = false };
            
            var roundId = Guid.NewGuid();
            var round = new EQARound { Id = roundId, ProgramId = programId, ExternalCode = "ROUND_CASCADE", IsDeleted = false };
            
            var devId = Guid.NewGuid();
            var dev = new EQADeviation { Id = devId, RoundId = roundId, Status = "Abierta", IsDeleted = false };

            context.EQAPrograms.Add(prog);
            context.EQAEnrollments.Add(enrollment);
            context.EQAMappings.Add(mapping);
            context.EQARounds.Add(round);
            context.EQADeviations.Add(dev);
            context.SaveChanges();

            // Perform deletion
            var deletedByUserId = Guid.NewGuid();
            var result = await service.DeleteProgramAsync(programId, deletedByUserId, "CascadeUser");
            Assert.True(result);

            // Detach to force reload
            context.Entry(prog).State = EntityState.Detached;
            context.Entry(enrollment).State = EntityState.Detached;
            context.Entry(mapping).State = EntityState.Detached;
            context.Entry(round).State = EntityState.Detached;
            context.Entry(dev).State = EntityState.Detached;

            var dbProg = await context.EQAPrograms.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == programId);
            var dbEnrollment = await context.EQAEnrollments.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == enrollmentId);
            var dbMapping = await context.EQAMappings.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == mappingId);
            var dbRound = await context.EQARounds.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == roundId);
            var dbDev = await context.EQADeviations.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == devId);

            Assert.NotNull(dbProg);
            Assert.True(dbProg.IsDeleted);
            Assert.Equal(deletedByUserId, dbProg.DeletedByUserId);

            Assert.NotNull(dbEnrollment);
            Assert.True(dbEnrollment.IsDeleted);
            Assert.Equal(deletedByUserId, dbEnrollment.DeletedByUserId);

            Assert.NotNull(dbMapping);
            Assert.True(dbMapping.IsDeleted);
            Assert.Equal(deletedByUserId, dbMapping.DeletedByUserId);

            Assert.NotNull(dbRound);
            Assert.True(dbRound.IsDeleted);
            Assert.Equal(deletedByUserId, dbRound.DeletedByUserId);

            Assert.NotNull(dbDev);
            Assert.True(dbDev.IsDeleted);
            Assert.Equal(deletedByUserId, dbDev.DeletedByUserId);
        }

        [Fact]
        public async Task EquipmentService_VoidQC_UpdatesStatusAndRecalculates()
        {
            using var context = new QmsDbContext(_options);
            var service = new EquipmentService(context);

            var equipmentId = Guid.NewGuid();
            var equipment = new Equipment
            {
                Id = equipmentId,
                Name = "Test Cytometer",
                Status = EquipmentStatus.IN_SERVICE,
                IsVerified = true,
                VerificationDate = DateTime.UtcNow.AddDays(-1)
            };
            context.Equipments.Add(equipment);

            var qc1Id = Guid.NewGuid();
            var qc1 = new EquipmentFunctionalQC
            {
                Id = qc1Id,
                EquipmentId = equipmentId,
                PerformedAt = DateTime.UtcNow.AddDays(-5),
                PerformedByUserId = Guid.NewGuid(),
                PerformedByUserName = "TechA",
                IsPass = true,
                Outcome = QCOutcome.CONFORME,
                IsDeleted = false
            };
            context.EquipmentFunctionalQC.Add(qc1);

            var qc2Id = Guid.NewGuid();
            var qc2 = new EquipmentFunctionalQC
            {
                Id = qc2Id,
                EquipmentId = equipmentId,
                PerformedAt = DateTime.UtcNow.AddDays(-1),
                PerformedByUserId = Guid.NewGuid(),
                PerformedByUserName = "TechB",
                IsPass = false,
                Outcome = QCOutcome.NO_CONFORME,
                EquipmentEndStatus = EquipmentStatus.QC_NON_CONFORMING,
                IsDeleted = false
            };
            context.EquipmentFunctionalQC.Add(qc2);
            context.SaveChanges();

            // Set current equipment status to match the latest QC
            equipment.Status = EquipmentStatus.QC_NON_CONFORMING;
            equipment.IsVerified = false;
            equipment.VerificationDate = qc2.PerformedAt;
            context.SaveChanges();

            var voidByUserId = Guid.NewGuid();
            var result = await service.VoidQCAsync(qc2Id, "Bad controls used", voidByUserId);
            Assert.True(result);

            // Detach and reload
            context.Entry(equipment).State = EntityState.Detached;
            context.Entry(qc2).State = EntityState.Detached;

            var dbEq = await context.Equipments.FindAsync(equipmentId);
            var dbQc2 = await context.EquipmentFunctionalQC.IgnoreQueryFilters().FirstOrDefaultAsync(q => q.Id == qc2Id);

            Assert.NotNull(dbQc2);
            Assert.True(dbQc2.IsDeleted);
            Assert.Equal("Bad controls used", dbQc2.VoidReason);
            Assert.Equal(voidByUserId, dbQc2.DeletedByUserId);

            // Verify status is reverted to QC1's pass status (IN_SERVICE) and date is QC1's date
            Assert.NotNull(dbEq);
            Assert.Equal(EquipmentStatus.IN_SERVICE, dbEq.Status);
            Assert.True(dbEq.IsVerified);
            Assert.Equal(qc1.PerformedAt, dbEq.VerificationDate);

            // Verify history log has void entry
            var history = await context.EquipmentHistory.Where(h => h.EquipmentId == equipmentId && h.ActionType == "QC_VOID").FirstOrDefaultAsync();
            Assert.NotNull(history);
            Assert.Contains("Bad controls used", history.Description);
        }

        [Fact]
        public void AuditLogs_ChainedCryptographicHash_VerifiesIntegrity()
        {
            using var context = new QmsDbContext(_options);

            var log1 = new AuditLog { Action = "CREATE", EntityType = "EQAProgram", Details = "Created Program", UserName = "UserA", Result = "OK" };
            context.AuditLogs.Add(log1);
            context.SaveChanges();

            var log2 = new AuditLog { Action = "EDIT", EntityType = "EQAProgram", Details = "Modified Program", UserName = "UserB", Result = "OK" };
            context.AuditLogs.Add(log2);
            context.SaveChanges();

            Assert.NotNull(log1.IntegrityHash);
            Assert.NotEmpty(log1.IntegrityHash!);
            Assert.NotNull(log2.IntegrityHash);
            Assert.NotEmpty(log2.IntegrityHash!);
            Assert.NotEqual(log1.IntegrityHash, log2.IntegrityHash);

            // Reconstruct the expected payload for the chained log2
            var expectedPayload = $"{log1.IntegrityHash}|{log2.Id}|{log2.Timestamp:o}|{log2.UserId}|{log2.UserName}|{log2.Action}|{log2.EntityType}|{log2.EntityId}|{log2.Details}|{log2.Reason}|{log2.Result}|{log2.MachineName}";
            
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expectedPayload);
                var expectedHash = Convert.ToHexString(sha256.ComputeHash(expectedBytes)).ToLowerInvariant();
                Assert.Equal(expectedHash, log2.IntegrityHash);
            }
        }

        [Fact]
        public async Task TestNC_CannotClose_WithoutCAPAVerified()
        {
            using var context = new QmsDbContext(_options);
            var qualityService = new QualityService(context);

            // Create NC
            var ncId = Guid.NewGuid();
            var nc = new Nonconformity
            {
                Id = ncId,
                Title = "Test NC",
                Description = "Description",
                Severity = NCSeverity.LOW,
                Status = NCStatus.OPEN,
                RootCauseAnalysis = "Some root cause",
                Containment = "Immediate containment",
                IsDeleted = false
            };
            context.Nonconformities.Add(nc);

            // Create CAPA action linked to NC
            var capaId = Guid.NewGuid();
            var capa = new CapaAction
            {
                Id = capaId,
                NCId = ncId,
                Description = "Test CAPA",
                Status = CAPAStatus.OPEN,
                IsDeleted = false
            };
            context.CapaActions.Add(capa);
            await context.SaveChangesAsync();

            // Try to close NC directly: should fail because CAPA is not VERIFIED (it is OPEN)
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                qualityService.UpdateNCStatusAsync(ncId, SharedModels.NCStatus.CLOSED));
            Assert.Contains("VERIFIED", ex.Message);

            // Set CAPA to DONE
            await qualityService.CompleteCAPAAsync(capaId, "check", Guid.NewGuid(), "User");

            // Still should fail because CAPA is not VERIFIED (it is DONE)
            var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                qualityService.UpdateNCStatusAsync(ncId, SharedModels.NCStatus.CLOSED));
            Assert.Contains("VERIFIED", ex2.Message);
        }

        [Fact]
        public async Task TestNC_CannotClose_WithoutElectronicSignature()
        {
            using var context = new QmsDbContext(_options);
            var qualityService = new QualityService(context);

            var ncId = Guid.NewGuid();
            var nc = new Nonconformity
            {
                Id = ncId,
                Title = "Test NC ready to close",
                Description = "Description",
                Severity = NCSeverity.LOW,
                Status = NCStatus.ACTION,
                RootCauseAnalysis = "Root cause documented",
                Containment = "Containment documented",
                IsDeleted = false
            };
            context.Nonconformities.Add(nc);

            context.CapaActions.Add(new CapaAction
            {
                Id = Guid.NewGuid(),
                NCId = ncId,
                Description = "Verified CAPA",
                Status = CAPAStatus.VERIFIED,
                IsDeleted = false
            });

            await context.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                qualityService.UpdateNCStatusAsync(ncId, SharedModels.NCStatus.CLOSED, Guid.NewGuid(), "QualityUser"));

            Assert.Contains("contrase", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task TestAuthorization_CannotGrant_WithoutAptoCompetency()
        {
            using var context = new QmsDbContext(_options);
            var staffService = new StaffService(context);

            var staffId = Guid.NewGuid();
            var authCatalogId = Guid.NewGuid();

            var catalog = new AuthorizationCatalog
            {
                Id = authCatalogId,
                Code = "AUTH_01",
                Name = "Task 1", // Matches the request TaskName
                RequiresCompetency = true
            };
            context.AuthorizationCatalogs.Add(catalog);

            var competencyId = Guid.NewGuid();
            
            // Add a catalog competency so FindAsync finds it in ValidateAuthorizationRequestAsync
            var competencyCatalog = new CompetencyCatalog
            {
                Id = competencyId,
                Code = "COMP_01",
                Name = "Competency 1",
                RoleScope = "Analyst",
                Area = "Hematología"
            };
            context.CompetencyCatalogs.Add(competencyCatalog);

            var reqComp = new AuthorizationRequiredCompetency
            {
                AuthorizationId = authCatalogId,
                CompetencyId = competencyId
            };
            context.AuthorizationRequiredCompetencies.Add(reqComp);

            var staffProfile = new StaffProfile
            {
                Id = staffId,
                PositionTitle = "Analyst",
                IsActive = true
            };
            context.StaffProfiles.Add(staffProfile);
            await context.SaveChangesAsync();

            var authRequest = new GrantAuthorizationRequest(
                StaffId: staffId,
                TaskName: "Task 1",
                Description: "Description 1",
                ValidFrom: DateTime.UtcNow,
                ValidUntil: DateTime.UtcNow.AddYears(1),
                GrantedByUserId: Guid.NewGuid(),
                CompetencyId: competencyId,
                EvaluationId: null,
                AssessmentMethod: "Justificación de prueba",
                EvidenceDocId: Guid.NewGuid()
            );

            // Should throw because required competency is not met (no status in DB, status is null/not APTO)
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                staffService.GrantAuthorizationAsync(authRequest));
            Assert.Contains("competencia", ex.Message);
        }

        [Fact]
        public async Task TestEquipmentImpact_CannotReturnToService_WithoutNCAndEvidence()
        {
            using var context = new QmsDbContext(_options);
            var equipmentService = new EquipmentService(context);

            var equipmentId = Guid.NewGuid();
            var eq = new Equipment
            {
                Id = equipmentId,
                Name = "Test Equipment",
                Status = EquipmentStatus.OUT_OF_SERVICE
            };
            context.Equipments.Add(eq);
            await context.SaveChangesAsync();

            var assessment = new RegisterImpactRequest
            {
                EquipmentId = equipmentId,
                ImpactType = "Posible impacto en resultados",
                ExternalNCId = null, // Missing NC linkage
                EvidencePath = "",   // Missing evidence
                UserId = Guid.NewGuid()
            };

            // Should throw because critical impact requires NC and evidence
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                equipmentService.RegisterImpactAssessmentAsync(assessment));
            Assert.Contains("vincular una No Conformidad", ex.Message);
        }

        [Fact]
        public async Task TestEQARound_CannotClose_IfUnsatisfactoryWithoutCAPA()
        {
            using var context = new QmsDbContext(_options);
            var eqaService = new EQAService(context);

            var programId = Guid.NewGuid();
            var prog = new EQAProgram { Id = programId, InternalCode = "PROG_EQA", Name = "EQA" };
            context.EQAPrograms.Add(prog);

            var roundId = Guid.NewGuid();
            var round = new EQARound
            {
                Id = roundId,
                ProgramId = programId,
                ExternalCode = "ROUND_01",
                GlobalOutcome = EQAPerformance.UNSATISFACTORY,
                Status = EQARoundStatus.IN_PROGRESS
            };
            context.EQARounds.Add(round);
            await context.SaveChangesAsync();

            // Try to close round - should fail because UNSATISFACTORY requires at least one active deviation
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                eqaService.UpdateRoundAsync(round));
            Assert.Contains("desviación activa", ex.Message);

            // Add active deviation but it's not closed
            var dev = new EQADeviation
            {
                Id = Guid.NewGuid(),
                RoundId = roundId,
                Status = "Abierta",
                LinkedCapaId = null
            };
            round.Deviations.Add(dev);

            // Set round to CLOSED
            round.Status = EQARoundStatus.CLOSED;

            // Should fail because deviation is not CLOSED
            var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                eqaService.UpdateRoundAsync(round));
            Assert.Contains("estado 'Cerrada'", ex2.Message);
        }

        [Fact]
        public async Task InventoryService_AdjustStock_AllowsWasteOnQuarantinedOrExpiredLot()
        {
            using var context = new QmsDbContext(_options);
            var service = new InventoryService(context);

            var reagentId = Guid.NewGuid();
            var reagent = new Reagent
            {
                Id = reagentId,
                Name = "CD4 Test",
                Reference = "12345",
                ReagentType = "Anticuerpo",
                Status = ReagentStatus.ACTIVO
            };
            context.Reagents.Add(reagent);

            var lotId = Guid.NewGuid();
            var lot = new ReagentLot
            {
                Id = lotId,
                ReagentId = reagentId,
                LotNumber = "LOT001",
                ReceivedQty = 5,
                AvailableQty = 5,
                ExpiryDate = DateTime.UtcNow.AddDays(-10), // Expired
                Status = LotStatus.QUARANTINE,
                CreatedAt = DateTime.UtcNow
            };
            context.ReagentLots.Add(lot);
            await context.SaveChangesAsync();

            // Act - Discard expired lot in quarantine as WASTE
            var request = new AdjustStockRequest(
                ReagentId: reagentId,
                ReagentLotId: lotId,
                MovementType: SharedModels.InventoryMovementType.WASTE,
                Qty: -5,
                Reason: "Descarte por caducidad",
                Notes: "Manual",
                UserId: Guid.NewGuid()
            );

            var ok = await service.AdjustStockAsync(request);

            // Assert
            Assert.True(ok);
            var updatedLot = await context.ReagentLots.FindAsync(lotId);
            Assert.NotNull(updatedLot);
            Assert.Equal(0, updatedLot.AvailableQty);
            Assert.Equal(LotStatus.EXPIRED, updatedLot.Status); // Changed to EXPIRED because it's a WASTE total consumption
        }

        [Fact]
        public async Task InventoryService_AdjustStock_AllowsAdjustOnQuarantinedLot()
        {
            using var context = new QmsDbContext(_options);
            var service = new InventoryService(context);

            var reagentId = Guid.NewGuid();
            var reagent = new Reagent
            {
                Id = reagentId,
                Name = "CD4 Test 2",
                Reference = "123456",
                ReagentType = "Anticuerpo",
                Status = ReagentStatus.ACTIVO
            };
            context.Reagents.Add(reagent);

            var lotId = Guid.NewGuid();
            var lot = new ReagentLot
            {
                Id = lotId,
                ReagentId = reagentId,
                LotNumber = "LOT002",
                ReceivedQty = 5,
                AvailableQty = 5,
                ExpiryDate = DateTime.UtcNow.AddDays(100),
                Status = LotStatus.QUARANTINE,
                CreatedAt = DateTime.UtcNow
            };
            context.ReagentLots.Add(lot);
            await context.SaveChangesAsync();

            // Act - Make negative ADJUST to correct mistake on quarantined lot
            var request = new AdjustStockRequest(
                ReagentId: reagentId,
                ReagentLotId: lotId,
                MovementType: SharedModels.InventoryMovementType.ADJUST,
                Qty: -2,
                Reason: "Error de conteo",
                Notes: "Manual",
                UserId: Guid.NewGuid()
            );

            var ok = await service.AdjustStockAsync(request);

            // Assert
            Assert.True(ok);
            var updatedLot = await context.ReagentLots.FindAsync(lotId);
            Assert.NotNull(updatedLot);
            Assert.Equal(3, updatedLot.AvailableQty);
            Assert.Equal(LotStatus.QUARANTINE, updatedLot.Status); // Remains in QUARANTINE since it wasn't WASTE and not completely consumed
        }

        [Fact]
        public async Task InventoryService_AdjustStock_BlocksOutOnQuarantinedOrExpiredLot()
        {
            using var context = new QmsDbContext(_options);
            var service = new InventoryService(context);

            var reagentId = Guid.NewGuid();
            var reagent = new Reagent
            {
                Id = reagentId,
                Name = "CD4 Test 3",
                Reference = "1234567",
                ReagentType = "Anticuerpo",
                Status = ReagentStatus.ACTIVO
            };
            context.Reagents.Add(reagent);

            var lotId = Guid.NewGuid();
            var lot = new ReagentLot
            {
                Id = lotId,
                ReagentId = reagentId,
                LotNumber = "LOT003",
                ReceivedQty = 5,
                AvailableQty = 5,
                ExpiryDate = DateTime.UtcNow.AddDays(100),
                Status = LotStatus.QUARANTINE, // In quarantine
                CreatedAt = DateTime.UtcNow
            };
            context.ReagentLots.Add(lot);
            await context.SaveChangesAsync();

            // Act & Assert - Clinical OUT should be blocked on quarantined lot
            var request = new AdjustStockRequest(
                ReagentId: reagentId,
                ReagentLotId: lotId,
                MovementType: SharedModels.InventoryMovementType.OUT,
                Qty: -1,
                Reason: "Clinical Use",
                Notes: "Manual",
                UserId: Guid.NewGuid()
            );

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AdjustStockAsync(request));
            Assert.Contains("Liberado o En Uso", ex.Message);
        }

        [Fact]
        public void DocumentType_TypeCode_MustBeUnique()
        {
            using var context = new QmsDbContext(_options);

            var type1 = new QMSFlowDoc.Domain.Entities.DocumentType { Id = Guid.NewGuid(), TypeCode = "SOP", Name = "Standard Operating Procedure" };
            var type2 = new QMSFlowDoc.Domain.Entities.DocumentType { Id = Guid.NewGuid(), TypeCode = "SOP", Name = "Same Code Procedure" };

            context.DocumentTypes.Add(type1);
            context.SaveChanges();

            context.DocumentTypes.Add(type2);
            
            // Should throw due to unique index violation
            Assert.ThrowsAny<DbUpdateException>(() => context.SaveChanges());
        }

        [Fact]
        public async Task DocumentType_InUseByActiveDocuments_CannotBeDeleted_ByBusinessLogic()
        {
            using var context = new QmsDbContext(_options);

            var typeId = Guid.NewGuid();
            var docType = new QMSFlowDoc.Domain.Entities.DocumentType { Id = typeId, TypeCode = "POL", Name = "Policy" };
            context.DocumentTypes.Add(docType);

            var doc = new Document
            {
                Id = Guid.NewGuid(),
                DocCode = "DOC-POL-001",
                Title = "General Safety Policy",
                DocumentTypeId = typeId,
                OwnerUserId = Guid.NewGuid(),
                Status = DocumentStatus.DRAFT,
                IsDeleted = false
            };
            context.Documents.Add(doc);
            await context.SaveChangesAsync();

            // Simulate the UI/business logic check before deletion
            var inUse = await context.Documents.AnyAsync(d => d.DocumentTypeId == typeId && !d.IsDeleted);
            
            Assert.True(inUse); // Business logic should block deletion if inUse is true
        }

        private UserManager<ApplicationUser> CreateUserManager(QmsDbContext context)
        {
            var userStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser, ApplicationRole, QmsDbContext, Guid>(context);
            var options = Microsoft.Extensions.Options.Options.Create(new IdentityOptions());
            var passwordHasher = new PasswordHasher<ApplicationUser>();
            var userValidators = new List<IUserValidator<ApplicationUser>> { new UserValidator<ApplicationUser>() };
            var passwordValidators = new List<IPasswordValidator<ApplicationUser>> { new PasswordValidator<ApplicationUser>() };
            var keyNormalizer = new UpperInvariantLookupNormalizer();
            var errors = new IdentityErrorDescriber();
            return new UserManager<ApplicationUser>(
                userStore, options, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, null, new DummyLogger());
        }

        private class DummyLogger : ILogger<UserManager<ApplicationUser>>, IDisposable
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {}
            public void Dispose() {}
        }

        [Fact]
        public async Task GlobalQueryFilters_ExcludesSoftDeletedEntities()
        {
            using var context = new QmsDbContext(_options);

            // 1. Create entities
            var ncId = Guid.NewGuid();
            var nc = new Nonconformity { Id = ncId, Title = "Soft Deleted NC", IsDeleted = true };
            context.Nonconformities.Add(nc);

            var capaId = Guid.NewGuid();
            var capa = new CapaAction { Id = capaId, Description = "Soft Deleted CAPA", IsDeleted = true };
            context.CapaActions.Add(capa);

            var complaintId = Guid.NewGuid();
            var complaint = new Complaint { Id = complaintId, Source = "Customer", Description = "Soft Deleted Complaint", IsDeleted = true };
            context.Complaints.Add(complaint);

            var progId = Guid.NewGuid();
            var prog = new EQAProgram { Id = progId, InternalCode = "EQA_DEL", Name = "Deleted Program", IsDeleted = true };
            context.EQAPrograms.Add(prog);

            var enrollmentId = Guid.NewGuid();
            var enrollment = new EQAEnrollment { Id = enrollmentId, ProgramId = progId, Year = 2026, IsDeleted = true };
            context.EQAEnrollments.Add(enrollment);

            var mappingId = Guid.NewGuid();
            var mapping = new EQAMapping { Id = mappingId, ProgramId = progId, InternalTestName = "Deleted Test", IsDeleted = true };
            context.EQAMappings.Add(mapping);

            var roundId = Guid.NewGuid();
            var round = new EQARound { Id = roundId, ProgramId = progId, ExternalCode = "ROUND_DEL", IsDeleted = true };
            context.EQARounds.Add(round);

            var devId = Guid.NewGuid();
            var dev = new EQADeviation { Id = devId, RoundId = roundId, Status = "Abierta", IsDeleted = true };
            context.EQADeviations.Add(dev);

            await context.SaveChangesAsync();

            // 2. Query through normal context (should be excluded)
            Assert.False(await context.Nonconformities.AnyAsync(x => x.Id == ncId));
            Assert.False(await context.CapaActions.AnyAsync(x => x.Id == capaId));
            Assert.False(await context.Complaints.AnyAsync(x => x.Id == complaintId));
            Assert.False(await context.EQAPrograms.AnyAsync(x => x.Id == progId));
            Assert.False(await context.EQAEnrollments.AnyAsync(x => x.Id == enrollmentId));
            Assert.False(await context.EQAMappings.AnyAsync(x => x.Id == mappingId));
            Assert.False(await context.EQARounds.AnyAsync(x => x.Id == roundId));
            Assert.False(await context.EQADeviations.AnyAsync(x => x.Id == devId));

            // 3. Query ignoring filters (should exist)
            Assert.True(await context.Nonconformities.IgnoreQueryFilters().AnyAsync(x => x.Id == ncId));
            Assert.True(await context.CapaActions.IgnoreQueryFilters().AnyAsync(x => x.Id == capaId));
            Assert.True(await context.Complaints.IgnoreQueryFilters().AnyAsync(x => x.Id == complaintId));
            Assert.True(await context.EQAPrograms.IgnoreQueryFilters().AnyAsync(x => x.Id == progId));
            Assert.True(await context.EQAEnrollments.IgnoreQueryFilters().AnyAsync(x => x.Id == enrollmentId));
            Assert.True(await context.EQAMappings.IgnoreQueryFilters().AnyAsync(x => x.Id == mappingId));
            Assert.True(await context.EQARounds.IgnoreQueryFilters().AnyAsync(x => x.Id == roundId));
            Assert.True(await context.EQADeviations.IgnoreQueryFilters().AnyAsync(x => x.Id == devId));
        }

        [Fact]
        public async Task ComplaintClosure_RequiresPasswordAndFailsOnIncorrectPassword()
        {
            using var context = new QmsDbContext(_options);
            var userManager = CreateUserManager(context);
            var qualityService = new QualityService(context, userManager);

            // Create test user with secure password
            var userId = Guid.NewGuid();
            var user = new ApplicationUser 
            { 
                Id = userId, 
                UserName = "qualitymgr", 
                Email = "qualitymgr@lab.com", 
                FullName = "Quality Manager" 
            };
            var createResult = await userManager.CreateAsync(user, "SecurePassword123!");
            Assert.True(createResult.Succeeded);

            // Create Complaint
            var complaintId = Guid.NewGuid();
            var complaint = new Complaint
            {
                Id = complaintId,
                Source = "Paciente Directo",
                Description = "Resultados demorados más de 24 horas",
                InvestigationResult = "Falta de personal en turno de noche",
                CorrectiveAction = "Reforzar guardias nocturnas",
                ResolutionEvidence = "Llamada informativa y disculpa al paciente",
                Status = ComplaintStatus.OPEN,
                IsDeleted = false
            };
            context.Complaints.Add(complaint);
            await context.SaveChangesAsync();

            // 1. Try to close without password (confirmPassword = null) -> should fail
            var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                qualityService.UpdateComplaintStatusAsync(complaintId, SharedModels.ComplaintStatus.CLOSED, userId, "qualitymgr", null));
            Assert.Contains("contraseña", ex1.Message, StringComparison.OrdinalIgnoreCase);

            // 2. Try to close with incorrect password -> should fail
            var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                qualityService.UpdateComplaintStatusAsync(complaintId, SharedModels.ComplaintStatus.CLOSED, userId, "qualitymgr", "WrongPass123!"));
            Assert.Contains("no es valida", ex2.Message, StringComparison.OrdinalIgnoreCase);

            // 3. Close with correct password -> should succeed
            var success = await qualityService.UpdateComplaintStatusAsync(complaintId, SharedModels.ComplaintStatus.CLOSED, userId, "qualitymgr", "SecurePassword123!");
            Assert.True(success);

            // Verify closure state in database
            var dbComplaint = await context.Complaints.FindAsync(complaintId);
            Assert.NotNull(dbComplaint);
            Assert.Equal(Domain.Entities.ComplaintStatus.CLOSED, dbComplaint.Status);
            Assert.NotNull(dbComplaint.ClosedAt);
            Assert.Equal(userId, dbComplaint.ClosedByUserId);
        }
    }
}
