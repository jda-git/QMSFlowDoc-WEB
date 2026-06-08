using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    public partial class AddIsoImprovementControls : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>("Opportunity", "Risks", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>("ActionPlan", "Risks", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>("Responsible", "Risks", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<DateTime>("DueDate", "Risks", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<int>("ResidualLikelihood", "Risks", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>("ResidualImpact", "Risks", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>("EffectivenessReview", "Risks", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<DateTime>("EffectivenessReviewDate", "Risks", type: "datetime2", nullable: true);

            migrationBuilder.AddColumn<string>("Objectives", "AuditPlans", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>("Criteria", "AuditPlans", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>("Conclusions", "AuditPlans", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>("FollowUpActions", "AuditPlans", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<DateTime>("CompletedAt", "AuditPlans", type: "datetime2", nullable: true);

            migrationBuilder.AddColumn<string>("Responsible", "AuditFindings", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<DateTime>("DueDate", "AuditFindings", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>("EffectivenessReview", "AuditFindings", type: "nvarchar(max)", nullable: true);

            migrationBuilder.AddColumn<string>("PreviousActionsReview", "ManagementReviews", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>("ChangesAffectingQms", "ManagementReviews", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>("ResourceNeeds", "ManagementReviews", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>("QualityIndicatorsReview", "ManagementReviews", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>("ExternalProviderPerformance", "ManagementReviews", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>("Decisions", "ManagementReviews", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>("ImprovementOpportunities", "ManagementReviews", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>("ActionOwner", "ManagementReviews", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<DateTime>("ActionDueDate", "ManagementReviews", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>("EffectivenessReview", "ManagementReviews", type: "nvarchar(max)", nullable: true);

            migrationBuilder.AddColumn<string>("EvaluatorName", "SupplierEvaluations", type: "nvarchar(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>("Criticality", "SupplierEvaluations", type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Media");
            migrationBuilder.AddColumn<string>("Scope", "SupplierEvaluations", type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>("Decision", "SupplierEvaluations", type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Aprobado");
            migrationBuilder.AddColumn<string>("CorrectiveActions", "SupplierEvaluations", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<DateTime>("NextEvaluationDate", "SupplierEvaluations", type: "datetime2", nullable: true);

            migrationBuilder.CreateTable(
                name: "QualityIndicators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TargetRule = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActualValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Trend = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MeetsTarget = table.Column<bool>(type: "bit", nullable: false),
                    Analysis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionPlan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Responsible = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectivenessReview = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EffectivenessReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityIndicators", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QualityIndicators_Name_Period",
                table: "QualityIndicators",
                columns: new[] { "Name", "Period" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "QualityIndicators");

            migrationBuilder.DropColumn("Opportunity", "Risks");
            migrationBuilder.DropColumn("ActionPlan", "Risks");
            migrationBuilder.DropColumn("Responsible", "Risks");
            migrationBuilder.DropColumn("DueDate", "Risks");
            migrationBuilder.DropColumn("ResidualLikelihood", "Risks");
            migrationBuilder.DropColumn("ResidualImpact", "Risks");
            migrationBuilder.DropColumn("EffectivenessReview", "Risks");
            migrationBuilder.DropColumn("EffectivenessReviewDate", "Risks");

            migrationBuilder.DropColumn("Objectives", "AuditPlans");
            migrationBuilder.DropColumn("Criteria", "AuditPlans");
            migrationBuilder.DropColumn("Conclusions", "AuditPlans");
            migrationBuilder.DropColumn("FollowUpActions", "AuditPlans");
            migrationBuilder.DropColumn("CompletedAt", "AuditPlans");

            migrationBuilder.DropColumn("Responsible", "AuditFindings");
            migrationBuilder.DropColumn("DueDate", "AuditFindings");
            migrationBuilder.DropColumn("EffectivenessReview", "AuditFindings");

            migrationBuilder.DropColumn("PreviousActionsReview", "ManagementReviews");
            migrationBuilder.DropColumn("ChangesAffectingQms", "ManagementReviews");
            migrationBuilder.DropColumn("ResourceNeeds", "ManagementReviews");
            migrationBuilder.DropColumn("QualityIndicatorsReview", "ManagementReviews");
            migrationBuilder.DropColumn("ExternalProviderPerformance", "ManagementReviews");
            migrationBuilder.DropColumn("Decisions", "ManagementReviews");
            migrationBuilder.DropColumn("ImprovementOpportunities", "ManagementReviews");
            migrationBuilder.DropColumn("ActionOwner", "ManagementReviews");
            migrationBuilder.DropColumn("ActionDueDate", "ManagementReviews");
            migrationBuilder.DropColumn("EffectivenessReview", "ManagementReviews");

            migrationBuilder.DropColumn("EvaluatorName", "SupplierEvaluations");
            migrationBuilder.DropColumn("Criticality", "SupplierEvaluations");
            migrationBuilder.DropColumn("Scope", "SupplierEvaluations");
            migrationBuilder.DropColumn("Decision", "SupplierEvaluations");
            migrationBuilder.DropColumn("CorrectiveActions", "SupplierEvaluations");
            migrationBuilder.DropColumn("NextEvaluationDate", "SupplierEvaluations");
        }
    }
}
