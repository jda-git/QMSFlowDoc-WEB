using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveAssessmentMethodToStaffAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssessmentMethod",
                table: "AuthorizationCatalogs");

            migrationBuilder.AddColumn<string>(
                name: "AssessmentMethod",
                table: "StaffAuthorizations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssessmentMethod",
                table: "StaffAuthorizations");

            migrationBuilder.AddColumn<string>(
                name: "AssessmentMethod",
                table: "AuthorizationCatalogs",
                type: "TEXT",
                nullable: true);
        }
    }
}
