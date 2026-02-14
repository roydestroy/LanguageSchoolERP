using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    public partial class AddEnrollmentProgramExtras : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludesStudyLab",
                table: "Enrollments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesTransportation",
                table: "Enrollments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "StudyLabMonthlyPrice",
                table: "Enrollments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TransportationMonthlyPrice",
                table: "Enrollments",
                type: "decimal(18,2)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludesStudyLab",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "IncludesTransportation",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "StudyLabMonthlyPrice",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "TransportationMonthlyPrice",
                table: "Enrollments");
        }
    }
}
