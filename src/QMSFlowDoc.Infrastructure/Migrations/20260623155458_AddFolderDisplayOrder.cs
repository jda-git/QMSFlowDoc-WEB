using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Folders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Folders");
        }
    }
}
