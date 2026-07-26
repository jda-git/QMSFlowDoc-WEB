using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMethodValidationIso15189Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "MethodVersions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSources",
                table: "MethodVersions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationScope",
                table: "MethodVersions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstructionsForUseVersion",
                table: "MethodVersions",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "MethodVersions",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudyDesign",
                table: "MethodVersions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationReportDocumentId",
                table: "MethodVersions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "MethodValidations",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Conclusion",
                table: "MethodValidations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSources",
                table: "MethodValidations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceDocumentId",
                table: "MethodValidations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SampleDescription",
                table: "MethodValidations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudyDesign",
                table: "MethodValidations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClinicalDecisionLimits",
                table: "Methods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataAnalysisProcedure",
                table: "Methods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EquipmentConfiguration",
                table: "Methods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntendedUse",
                table: "Methods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Limitations",
                table: "Methods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Measurands",
                table: "Methods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeasurementRange",
                table: "Methods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PanelDescription",
                table: "Methods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreAnalyticalRequirements",
                table: "Methods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegulatoryClassification",
                table: "Methods",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ResultType",
                table: "Methods",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewDueDate",
                table: "Methods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecimenTypes",
                table: "Methods",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CalculationMethod",
                table: "MeasurementUncertainties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClinicalUse",
                table: "MeasurementUncertainties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataPeriodEnd",
                table: "MeasurementUncertainties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataPeriodStart",
                table: "MeasurementUncertainties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSources",
                table: "MeasurementUncertainties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceDocumentId",
                table: "MeasurementUncertainties",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeasurementRange",
                table: "MeasurementUncertainties",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewDueDate",
                table: "MeasurementUncertainties",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "MethodVersions");

            migrationBuilder.DropColumn(
                name: "DataSources",
                table: "MethodVersions");

            migrationBuilder.DropColumn(
                name: "EvaluationScope",
                table: "MethodVersions");

            migrationBuilder.DropColumn(
                name: "InstructionsForUseVersion",
                table: "MethodVersions");

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "MethodVersions");

            migrationBuilder.DropColumn(
                name: "StudyDesign",
                table: "MethodVersions");

            migrationBuilder.DropColumn(
                name: "ValidationReportDocumentId",
                table: "MethodVersions");

            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "MethodValidations");

            migrationBuilder.DropColumn(
                name: "Conclusion",
                table: "MethodValidations");

            migrationBuilder.DropColumn(
                name: "DataSources",
                table: "MethodValidations");

            migrationBuilder.DropColumn(
                name: "EvidenceDocumentId",
                table: "MethodValidations");

            migrationBuilder.DropColumn(
                name: "SampleDescription",
                table: "MethodValidations");

            migrationBuilder.DropColumn(
                name: "StudyDesign",
                table: "MethodValidations");

            migrationBuilder.DropColumn(
                name: "ClinicalDecisionLimits",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "DataAnalysisProcedure",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "EquipmentConfiguration",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "IntendedUse",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "Limitations",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "Measurands",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "MeasurementRange",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "PanelDescription",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "PreAnalyticalRequirements",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "RegulatoryClassification",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "ResultType",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "ReviewDueDate",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "SpecimenTypes",
                table: "Methods");

            migrationBuilder.DropColumn(
                name: "CalculationMethod",
                table: "MeasurementUncertainties");

            migrationBuilder.DropColumn(
                name: "ClinicalUse",
                table: "MeasurementUncertainties");

            migrationBuilder.DropColumn(
                name: "DataPeriodEnd",
                table: "MeasurementUncertainties");

            migrationBuilder.DropColumn(
                name: "DataPeriodStart",
                table: "MeasurementUncertainties");

            migrationBuilder.DropColumn(
                name: "DataSources",
                table: "MeasurementUncertainties");

            migrationBuilder.DropColumn(
                name: "EvidenceDocumentId",
                table: "MeasurementUncertainties");

            migrationBuilder.DropColumn(
                name: "MeasurementRange",
                table: "MeasurementUncertainties");

            migrationBuilder.DropColumn(
                name: "ReviewDueDate",
                table: "MeasurementUncertainties");
        }
    }
}
