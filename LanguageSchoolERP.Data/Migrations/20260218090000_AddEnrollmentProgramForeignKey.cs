using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    public partial class AddEnrollmentProgramForeignKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgramId",
                table: "Enrollments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM Programs)
BEGIN
    INSERT INTO Programs (Name, HasTransport, HasStudyLab, HasBooks)
    VALUES ('Default Program', 0, 0, 0)
END
");

            migrationBuilder.Sql(@"
DECLARE @firstProgramId INT = (SELECT TOP(1) Id FROM Programs ORDER BY Id);
UPDATE Enrollments SET ProgramId = @firstProgramId WHERE ProgramId = 0;
");

            migrationBuilder.DropColumn(
                name: "ProgramType",
                table: "Enrollments");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_ProgramId",
                table: "Enrollments",
                column: "ProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_Enrollments_Programs_ProgramId",
                table: "Enrollments",
                column: "ProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Enrollments_Programs_ProgramId",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_ProgramId",
                table: "Enrollments");

            migrationBuilder.AddColumn<int>(
                name: "ProgramType",
                table: "Enrollments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "Enrollments");
        }
    }
}
