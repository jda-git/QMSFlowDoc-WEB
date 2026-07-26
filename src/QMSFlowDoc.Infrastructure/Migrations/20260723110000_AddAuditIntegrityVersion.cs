using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using QMSFlowDoc.Infrastructure.Persistence;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(QmsDbContext))]
    [Migration("20260723110000_AddAuditIntegrityVersion")]
    public partial class AddAuditIntegrityVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntegrityVersion",
                table: "AuditLogs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntegrityVersion",
                table: "AuditLogs");
        }
    }
}
