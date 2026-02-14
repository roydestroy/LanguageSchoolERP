using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentContractFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasStudyLab",
                table: "Enrollments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasTransportation",
                table: "Enrollments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "InstallmentDayOfMonth",
                table: "Enrollments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "StudyLabMonthlyFee",
                table: "Enrollments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TransportationMonthlyFee",
                table: "Enrollments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasStudyLab",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "HasTransportation",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "InstallmentDayOfMonth",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "StudyLabMonthlyFee",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "TransportationMonthlyFee",
                table: "Enrollments");
        }
    }
}
