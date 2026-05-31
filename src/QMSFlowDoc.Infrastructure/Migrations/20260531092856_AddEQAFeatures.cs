using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QMSFlowDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEQAFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EQAResults");

            migrationBuilder.RenameColumn(
                name: "CycleFrequency",
                table: "EQAPrograms",
                newName: "ResponsibleUserId");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "EQAPrograms",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "EQAPrograms",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CoordinatorEntity",
                table: "EQAPrograms",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CoveredTests",
                table: "EQAPrograms",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvaluatedParameters",
                table: "EQAPrograms",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ExpectedRoundsPerYear",
                table: "EQAPrograms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedSamplesPerRound",
                table: "EQAPrograms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GeneralAcceptanceCriteria",
                table: "EQAPrograms",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InternalCode",
                table: "EQAPrograms",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Periodicity",
                table: "EQAPrograms",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SampleType",
                table: "EQAPrograms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubArea",
                table: "EQAPrograms",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TargetResult",
                table: "EQAPrograms",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "EQAEnrollments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ProgramId = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConfirmationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ParticipantCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExternalPlatformUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ExternalUser = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    ResponsibleUserId = table.Column<string>(type: "TEXT", nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsCritical = table.Column<bool>(type: "INTEGER", nullable: false),
                    EvidenceFileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EvidenceFilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EQAEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EQAEnrollments_EQAPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "EQAPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EQAMappings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ProgramId = table.Column<string>(type: "TEXT", nullable: false),
                    MethodId = table.Column<string>(type: "TEXT", nullable: true),
                    InternalTestName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Panel = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    ResultType = table.Column<int>(type: "INTEGER", nullable: false),
                    CoverageLevel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Criticidad = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AlcanceAcreditado = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EQAMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EQAMappings_EQAPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "EQAPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EQARounds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ProgramId = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ExpectedReceiptDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RealReceiptDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SubmissionDeadline = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RealSubmissionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpectedReportDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RealReportDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RoundType = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    ReportFileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReportFilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CertificateFileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CertificateFilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    GlobalOutcome = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalScore = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    AssessorComments = table.Column<string>(type: "TEXT", nullable: true),
                    InternalNotes = table.Column<string>(type: "TEXT", nullable: true),
                    RequiresAction = table.Column<bool>(type: "INTEGER", nullable: false),
                    InternalEvaluationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EvaluatedByUserId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EQARounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EQARounds_EQAPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "EQAPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EQADeviations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    RoundId = table.Column<string>(type: "TEXT", nullable: false),
                    DeviationType = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    ProbableCause = table.Column<string>(type: "TEXT", nullable: false),
                    ClinicalImpact = table.Column<string>(type: "TEXT", nullable: false),
                    ActionTaken = table.Column<string>(type: "TEXT", nullable: false),
                    LinkedCapaId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EvidenceFileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EvidenceFilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    EffectivenessMethod = table.Column<string>(type: "TEXT", nullable: true),
                    EffectivenessOutcome = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    EffectivenessEvaluationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EffectivenessEvidencePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EQADeviations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EQADeviations_EQARounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "EQARounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EQASamples",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    RoundId = table.Column<string>(type: "TEXT", nullable: false),
                    InternalCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ExternalCode = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SampleType = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    ReceiptDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceiptStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Integrity = table.Column<int>(type: "INTEGER", nullable: false),
                    TempAtReceipt = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    TransportConditions = table.Column<string>(type: "TEXT", nullable: true),
                    ReceivedVolume = table.Column<decimal>(type: "decimal(10,4)", nullable: true),
                    Anticoagulant = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DownloadDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    FileFormat = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: true),
                    IntegrityChecksum = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AnalysisSoftware = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    SoftwareVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ProcessingStart = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProcessingEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProcessDescription = table.Column<string>(type: "TEXT", nullable: true),
                    EquipmentId = table.Column<string>(type: "TEXT", nullable: true),
                    FollowedRoutineProcedure = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeviationsJustification = table.Column<string>(type: "TEXT", nullable: true),
                    ProcessingEvidencePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SubmittedResultsJson = table.Column<string>(type: "TEXT", nullable: true),
                    SubmittedEvidencePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SubmittedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    SubmissionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EQASamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EQASamples_EQARounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "EQARounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EQADeviations_RoundId",
                table: "EQADeviations",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_EQAEnrollments_ProgramId",
                table: "EQAEnrollments",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_EQAMappings_ProgramId",
                table: "EQAMappings",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_EQARounds_ProgramId",
                table: "EQARounds",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_EQASamples_RoundId",
                table: "EQASamples",
                column: "RoundId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EQADeviations");

            migrationBuilder.DropTable(
                name: "EQAEnrollments");

            migrationBuilder.DropTable(
                name: "EQAMappings");

            migrationBuilder.DropTable(
                name: "EQASamples");

            migrationBuilder.DropTable(
                name: "EQARounds");

            migrationBuilder.DropColumn(
                name: "Area",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "CoordinatorEntity",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "CoveredTests",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "EvaluatedParameters",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "ExpectedRoundsPerYear",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "ExpectedSamplesPerRound",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "GeneralAcceptanceCriteria",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "InternalCode",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "Periodicity",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "SampleType",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "SubArea",
                table: "EQAPrograms");

            migrationBuilder.DropColumn(
                name: "TargetResult",
                table: "EQAPrograms");

            migrationBuilder.RenameColumn(
                name: "ResponsibleUserId",
                table: "EQAPrograms",
                newName: "CycleFrequency");

            migrationBuilder.AlterColumn<string>(
                name: "Provider",
                table: "EQAPrograms",
                type: "TEXT",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 300);

            migrationBuilder.CreateTable(
                name: "EQAResults",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CycleIdentifier = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EvidenceDocId = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Performance = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessingDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProgramId = table.Column<string>(type: "TEXT", nullable: false),
                    ReceiptDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewerUserId = table.Column<string>(type: "TEXT", nullable: true),
                    Score = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmissionDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EQAResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EQAResults_EQAPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "EQAPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EQAResults_ProgramId",
                table: "EQAResults",
                column: "ProgramId");
        }
    }
}
