using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentSchoolName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SchoolName",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SchoolName",
                table: "Students");
        }
    }
}
