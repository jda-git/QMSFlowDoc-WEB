using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;
using QMSFlowDoc.Infrastructure.Services.EQA;
using System;
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
    }
}
