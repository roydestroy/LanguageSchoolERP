using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    public partial class AddContractsFeature : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractTemplates",
                columns: table => new
                {
                    ContractTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BranchKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TemplateRelativePath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTemplates", x => x.ContractTemplateId);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "ContractTemplateId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: true);

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
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "Contracts",
                newName: "CreatedAt");

            migrationBuilder.AlterColumn<string>(
                name: "PdfPath",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.Sql(@"
DECLARE @FilotheiTemplateId uniqueidentifier = 'A31F05B7-39EE-4D70-8AB3-D55D6EA277E5';
DECLARE @NeaIoniaTemplateId uniqueidentifier = 'CC06F109-7D64-4D33-9F7D-D0A2DC7AF6F5';

IF NOT EXISTS (SELECT 1 FROM ContractTemplates WHERE ContractTemplateId = @FilotheiTemplateId)
BEGIN
    INSERT INTO ContractTemplates (ContractTemplateId, Name, BranchKey, TemplateRelativePath, IsActive)
    VALUES (@FilotheiTemplateId, N'Συμφωνητικό Φιλοθέη', N'FILOTHEI', N'Templates\\ΣΥΜΦΩΝΗΤΙΚΟ ΦΙΛΟΘΕΗ.dotm', 1);
END

IF NOT EXISTS (SELECT 1 FROM ContractTemplates WHERE ContractTemplateId = @NeaIoniaTemplateId)
BEGIN
    INSERT INTO ContractTemplates (ContractTemplateId, Name, BranchKey, TemplateRelativePath, IsActive)
    VALUES (@NeaIoniaTemplateId, N'Συμφωνητικό Νέα Ιωνία', N'NEA_IONIA', N'Templates\\ΣΥΜΦΩΝΗΤΙΚΟ ΝΕΑ ΙΩΝΙΑ.dotm', 1);
END

UPDATE c
SET c.StudentId = e.StudentId
FROM Contracts c
INNER JOIN Enrollments e ON e.EnrollmentId = c.EnrollmentId
WHERE c.StudentId IS NULL;

UPDATE Contracts
SET ContractTemplateId = @FilotheiTemplateId
WHERE ContractTemplateId IS NULL;
");

            migrationBuilder.DropColumn(
                name: "DocxPath",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Contracts");

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ContractTemplateId",
                table: "Contracts",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractTemplateId",
                table: "Contracts",
                column: "ContractTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_StudentId",
                table: "Contracts",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_ContractTemplates_ContractTemplateId",
                table: "Contracts",
                column: "ContractTemplateId",
                principalTable: "ContractTemplates",
                principalColumn: "ContractTemplateId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Students_StudentId",
                table: "Contracts",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "StudentId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_ContractTemplates_ContractTemplateId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Students_StudentId",
                table: "Contracts");

            migrationBuilder.DropTable(
                name: "ContractTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ContractTemplateId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_StudentId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ContractTemplateId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "DataJson",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "Contracts");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Contracts",
                newName: "CreatedDate");

            migrationBuilder.AlterColumn<string>(
                name: "PdfPath",
                table: "Contracts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

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
        }
    }
}
