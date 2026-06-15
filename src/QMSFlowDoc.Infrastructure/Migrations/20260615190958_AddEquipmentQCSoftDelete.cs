using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentQCSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "EquipmentFunctionalQC",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "EquipmentFunctionalQC",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "EquipmentFunctionalQC",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "EquipmentFunctionalQC",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "EquipmentFunctionalQC");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "EquipmentFunctionalQC");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "EquipmentFunctionalQC");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "EquipmentFunctionalQC");
        }
    }
}
