using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMethodApprovalWorkflowControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Characteristic",
                table: "MethodValidations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MethodVersions_MethodId_Version",
                table: "MethodVersions",
                columns: new[] { "MethodId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MethodVersions_MethodId_Version",
                table: "MethodVersions");

            migrationBuilder.DropColumn(
                name: "Characteristic",
                table: "MethodValidations");
        }
    }
}
