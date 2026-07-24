using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorrectNcSensorStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NC identifies a disconnected probe, not a measurement outside its configured range.
            migrationBuilder.Sql("UPDATE \"EnvironmentalReadings\" SET \"IsOutOfRange\" = 0 WHERE \"SensorNotConnected\" = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
