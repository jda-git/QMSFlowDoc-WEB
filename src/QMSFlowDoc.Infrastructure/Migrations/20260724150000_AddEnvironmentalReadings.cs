using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations;

[Migration("20260724150000_AddEnvironmentalReadings")]
public partial class AddEnvironmentalReadings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EnvironmentalReadings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                Source = table.Column<int>(type: "INTEGER", nullable: false),
                TemperatureCelsius = table.Column<decimal>(type: "TEXT", nullable: true),
                HumidityPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                SensorNotConnected = table.Column<bool>(type: "INTEGER", nullable: false),
                IsOutOfRange = table.Column<bool>(type: "INTEGER", nullable: false),
                RawLine = table.Column<string>(type: "TEXT", nullable: true),
                Event = table.Column<string>(type: "TEXT", nullable: true),
                ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ImportedByName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                ImportBatchId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_EnvironmentalReadings", x => x.Id));
        migrationBuilder.CreateIndex("IX_EnvironmentalReadings_RecordedAt_Source", "EnvironmentalReadings", new[] { "RecordedAt", "Source" });
        migrationBuilder.CreateIndex("IX_EnvironmentalReadings_ImportBatchId", "EnvironmentalReadings", "ImportBatchId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("EnvironmentalReadings");
}
