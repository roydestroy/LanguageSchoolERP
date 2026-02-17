using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    public partial class AddEnrollmentStopFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStopped",
                table: "Enrollments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StoppedOn",
                table: "Enrollments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StoppedAmountWaived",
                table: "Enrollments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "StopReason",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsStopped", table: "Enrollments");
            migrationBuilder.DropColumn(name: "StoppedOn", table: "Enrollments");
            migrationBuilder.DropColumn(name: "StoppedAmountWaived", table: "Enrollments");
            migrationBuilder.DropColumn(name: "StopReason", table: "Enrollments");
        }
    }
}
