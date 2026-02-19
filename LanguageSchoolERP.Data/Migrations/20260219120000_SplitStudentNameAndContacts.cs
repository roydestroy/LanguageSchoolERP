using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    public partial class SplitStudentNameAndContacts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "FirstName", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "LastName", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Mobile", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Landline", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "FatherEmail", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "FatherMobile", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "FatherLandline", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "MotherEmail", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "MotherMobile", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "MotherLandline", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE Students
SET 
    FirstName = CASE
        WHEN LTRIM(RTRIM(ISNULL(FullName, ''))) = '' THEN ''
        WHEN CHARINDEX(' ', LTRIM(RTRIM(FullName))) = 0 THEN LTRIM(RTRIM(FullName))
        ELSE LEFT(LTRIM(RTRIM(FullName)), LEN(LTRIM(RTRIM(FullName))) - CHARINDEX(' ', REVERSE(LTRIM(RTRIM(FullName)))))
    END,
    LastName = CASE
        WHEN LTRIM(RTRIM(ISNULL(FullName, ''))) = '' THEN ''
        WHEN CHARINDEX(' ', LTRIM(RTRIM(FullName))) = 0 THEN ''
        ELSE RIGHT(LTRIM(RTRIM(FullName)), CHARINDEX(' ', REVERSE(LTRIM(RTRIM(FullName)))) - 1)
    END,
    Mobile = CASE WHEN LEFT(ISNULL(Phone,''),1)='6' THEN ISNULL(Phone,'') ELSE '' END,
    Landline = CASE WHEN LEFT(ISNULL(Phone,''),1)='2' THEN ISNULL(Phone,'') ELSE '' END,
    FatherMobile = CASE WHEN LEFT(ISNULL(FatherContact,''),1)='6' THEN ISNULL(FatherContact,'') ELSE '' END,
    FatherLandline = CASE WHEN LEFT(ISNULL(FatherContact,''),1)='2' THEN ISNULL(FatherContact,'') ELSE '' END,
    MotherMobile = CASE WHEN LEFT(ISNULL(MotherContact,''),1)='6' THEN ISNULL(MotherContact,'') ELSE '' END,
    MotherLandline = CASE WHEN LEFT(ISNULL(MotherContact,''),1)='2' THEN ISNULL(MotherContact,'') ELSE '' END
");

            migrationBuilder.DropColumn(name: "FullName", table: "Students");
            migrationBuilder.DropColumn(name: "Phone", table: "Students");
            migrationBuilder.DropColumn(name: "FatherContact", table: "Students");
            migrationBuilder.DropColumn(name: "MotherContact", table: "Students");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "FullName", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Phone", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "FatherContact", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "MotherContact", table: "Students", type: "nvarchar(max)", nullable: false, defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE Students
SET 
    FullName = LTRIM(RTRIM(CONCAT(FirstName, ' ', LastName))),
    Phone = COALESCE(NULLIF(Mobile,''), Landline, ''),
    FatherContact = COALESCE(NULLIF(FatherMobile,''), FatherLandline, ''),
    MotherContact = COALESCE(NULLIF(MotherMobile,''), MotherLandline, '')
");

            migrationBuilder.DropColumn(name: "FirstName", table: "Students");
            migrationBuilder.DropColumn(name: "LastName", table: "Students");
            migrationBuilder.DropColumn(name: "Mobile", table: "Students");
            migrationBuilder.DropColumn(name: "Landline", table: "Students");
            migrationBuilder.DropColumn(name: "FatherEmail", table: "Students");
            migrationBuilder.DropColumn(name: "FatherMobile", table: "Students");
            migrationBuilder.DropColumn(name: "FatherLandline", table: "Students");
            migrationBuilder.DropColumn(name: "MotherEmail", table: "Students");
            migrationBuilder.DropColumn(name: "MotherMobile", table: "Students");
            migrationBuilder.DropColumn(name: "MotherLandline", table: "Students");
        }
    }
}
