using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierEvaluationEvidenceDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenceDocumentId",
                table: "SupplierEvaluations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierEvaluations_EvidenceDocumentId",
                table: "SupplierEvaluations",
                column: "EvidenceDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierEvaluations_Documents_EvidenceDocumentId",
                table: "SupplierEvaluations",
                column: "EvidenceDocumentId",
                principalTable: "Documents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierEvaluations_Documents_EvidenceDocumentId",
                table: "SupplierEvaluations");

            migrationBuilder.DropIndex(
                name: "IX_SupplierEvaluations_EvidenceDocumentId",
                table: "SupplierEvaluations");

            migrationBuilder.DropColumn(
                name: "EvidenceDocumentId",
                table: "SupplierEvaluations");
        }
    }
}
