using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations;

[Migration("20260724140000_AddImpartialityDeclarations")]
public partial class AddImpartialityDeclarations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ImpartialityDeclarations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DeclaredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                DeclarantName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Scope = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                Mitigation = table.Column<string>(type: "TEXT", nullable: true),
                ReviewerName = table.Column<string>(type: "TEXT", nullable: true),
                ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                NextReviewDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                Decision = table.Column<string>(type: "TEXT", nullable: true),
                EvidenceDocumentId = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImpartialityDeclarations", x => x.Id);
                table.ForeignKey("FK_ImpartialityDeclarations_Documents_EvidenceDocumentId", x => x.EvidenceDocumentId, "Documents", "Id", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex("IX_ImpartialityDeclarations_Status_NextReviewDate", "ImpartialityDeclarations", new[] { "Status", "NextReviewDate" });
        migrationBuilder.CreateIndex("IX_ImpartialityDeclarations_EvidenceDocumentId", "ImpartialityDeclarations", "EvidenceDocumentId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("ImpartialityDeclarations");
}
