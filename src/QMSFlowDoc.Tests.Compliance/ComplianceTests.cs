using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Domain.Entities;
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
            var dbRecord = await context.EQAPrograms.FindAsync(programId);
            Assert.NotNull(dbRecord);
            Assert.True(dbRecord.IsDeleted);
            Assert.NotNull(dbRecord.DeletedAt);
            Assert.Equal("AdminUser", context.AuditLogs.OrderByDescending(l => l.Timestamp).First().UserName);
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
    }
}
