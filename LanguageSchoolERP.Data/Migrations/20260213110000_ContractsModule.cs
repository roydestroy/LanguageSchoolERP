using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    public partial class ContractsModule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractTemplates",
                columns: table => new
                {
                    ContractTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TemplateFilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTemplates", x => x.ContractTemplateId);
                });

            var defaultTemplateId = Guid.NewGuid();

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Contracts",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "DataJson",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<Guid>(
                name: "StudentId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: defaultTemplateId);

            migrationBuilder.Sql($@"
                INSERT INTO ContractTemplates (ContractTemplateId, Name, BranchKey, TemplateFilePath, IsActive)
                VALUES ('{defaultTemplateId}', 'Migrated Template', 'Filothei', 'Templates/Contracts/migrated-default.docx', 1);

                UPDATE c
                SET c.StudentId = e.StudentId,
                    c.TemplateId = '{defaultTemplateId}',
                    c.CreatedAt = c.CreatedDate,
                    c.DataJson = '{{}}'
                FROM Contracts c
                INNER JOIN Enrollments e ON e.EnrollmentId = c.EnrollmentId;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "PdfPath",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DocxPath",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Contracts");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_StudentId",
                table: "Contracts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_TemplateId",
                table: "Contracts",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_ContractTemplates_TemplateId",
                table: "Contracts",
                column: "TemplateId",
                principalTable: "ContractTemplates",
                principalColumn: "ContractTemplateId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Students_StudentId",
                table: "Contracts",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "StudentId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_ContractTemplates_TemplateId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Students_StudentId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_StudentId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_TemplateId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DataJson",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Contracts");

            migrationBuilder.AlterColumn<string>(
                name: "PdfPath",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Contracts",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "DocxPath",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Contracts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.DropTable(
                name: "ContractTemplates");
        }
    }
}
