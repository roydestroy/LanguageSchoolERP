using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    public partial class AddPerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AcademicPeriods_Name",
                table: "AcademicPeriods",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_AcademicPeriodId_IsStopped",
                table: "Enrollments",
                columns: new[] { "AcademicPeriodId", "IsStopped" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId_AcademicPeriodId",
                table: "Enrollments",
                columns: new[] { "StudentId", "AcademicPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentDate",
                table: "Payments",
                column: "PaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_Programs_Name",
                table: "Programs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_LastName_FirstName",
                table: "Students",
                columns: new[] { "LastName", "FirstName" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AcademicPeriods_Name",
                table: "AcademicPeriods");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_AcademicPeriodId_IsStopped",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_StudentId_AcademicPeriodId",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PaymentDate",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Programs_Name",
                table: "Programs");

            migrationBuilder.DropIndex(
                name: "IX_Students_LastName_FirstName",
                table: "Students");
        }
    }
}
