using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureMonitoringTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "EnvironmentalReadings" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_EnvironmentalReadings" PRIMARY KEY,
                    "RecordedAt" TEXT NOT NULL, "Source" INTEGER NOT NULL,
                    "TemperatureCelsius" TEXT NULL, "HumidityPercent" TEXT NULL,
                    "SensorNotConnected" INTEGER NOT NULL, "IsOutOfRange" INTEGER NOT NULL,
                    "RawLine" TEXT NULL, "Event" TEXT NULL, "ImportedAt" TEXT NOT NULL,
                    "ImportedByName" TEXT NOT NULL, "ImportBatchId" TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_EnvironmentalReadings_RecordedAt_Source"
                ON "EnvironmentalReadings" ("RecordedAt", "Source");
                CREATE INDEX IF NOT EXISTS "IX_EnvironmentalReadings_ImportBatchId"
                ON "EnvironmentalReadings" ("ImportBatchId");
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "ImpartialityDeclarations" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_ImpartialityDeclarations" PRIMARY KEY,
                    "DeclaredAt" TEXT NOT NULL, "DeclarantName" TEXT NOT NULL,
                    "Scope" TEXT NOT NULL, "Description" TEXT NOT NULL, "Mitigation" TEXT NULL,
                    "ReviewerName" TEXT NULL, "ReviewedAt" TEXT NULL, "NextReviewDate" TEXT NULL,
                    "Status" INTEGER NOT NULL, "Decision" TEXT NULL, "EvidenceDocumentId" TEXT NULL,
                    CONSTRAINT "FK_ImpartialityDeclarations_Documents_EvidenceDocumentId"
                    FOREIGN KEY ("EvidenceDocumentId") REFERENCES "Documents" ("Id") ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_ImpartialityDeclarations_EvidenceDocumentId"
                ON "ImpartialityDeclarations" ("EvidenceDocumentId");
                CREATE INDEX IF NOT EXISTS "IX_ImpartialityDeclarations_Status_NextReviewDate"
                ON "ImpartialityDeclarations" ("Status", "NextReviewDate");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
