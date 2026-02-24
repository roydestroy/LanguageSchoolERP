using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentNormalizedNameIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedFirstName",
                table: "Students",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedLastName",
                table: "Students",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE Students SET NormalizedFirstName = UPPER(ISNULL(FirstName, '')), NormalizedLastName = UPPER(ISNULL(LastName, ''));");

            migrationBuilder.CreateIndex(
                name: "IX_Students_NormalizedLastName_NormalizedFirstName",
                table: "Students",
                columns: new[] { "NormalizedLastName", "NormalizedFirstName" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_NormalizedLastName_NormalizedFirstName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "NormalizedFirstName",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "NormalizedLastName",
                table: "Students");
        }
    }
}
