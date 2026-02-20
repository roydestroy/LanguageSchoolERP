using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    [DbContext(typeof(SchoolDbContext))]
    [Migration("20260220135100_FixStudentSchemaForRemoteImport")]
    public partial class FixStudentSchemaForRemoteImport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'FirstName') IS NULL ALTER TABLE [Students] ADD [FirstName] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_FirstName] DEFAULT N'';");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'LastName') IS NULL ALTER TABLE [Students] ADD [LastName] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_LastName] DEFAULT N'';");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'Mobile') IS NULL ALTER TABLE [Students] ADD [Mobile] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_Mobile] DEFAULT N'';");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'Landline') IS NULL ALTER TABLE [Students] ADD [Landline] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_Landline] DEFAULT N'';");

            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'FatherEmail') IS NULL ALTER TABLE [Students] ADD [FatherEmail] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_FatherEmail] DEFAULT N'';");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'FatherMobile') IS NULL ALTER TABLE [Students] ADD [FatherMobile] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_FatherMobile] DEFAULT N'';");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'FatherLandline') IS NULL ALTER TABLE [Students] ADD [FatherLandline] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_FatherLandline] DEFAULT N'';");

            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'MotherEmail') IS NULL ALTER TABLE [Students] ADD [MotherEmail] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_MotherEmail] DEFAULT N'';");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'MotherMobile') IS NULL ALTER TABLE [Students] ADD [MotherMobile] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_MotherMobile] DEFAULT N'';");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'MotherLandline') IS NULL ALTER TABLE [Students] ADD [MotherLandline] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_MotherLandline] DEFAULT N'';");

            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'PreferredPhoneSource') IS NULL ALTER TABLE [Students] ADD [PreferredPhoneSource] int NOT NULL CONSTRAINT [DF_Students_PreferredPhoneSource] DEFAULT 0;");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'PreferredEmailSource') IS NULL ALTER TABLE [Students] ADD [PreferredEmailSource] int NOT NULL CONSTRAINT [DF_Students_PreferredEmailSource] DEFAULT 0;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('Students', 'FullName') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE [Students]
        SET [FirstName] = CASE
                WHEN ISNULL(LTRIM(RTRIM([FirstName])), N'''') = N'''' THEN
                    CASE
                        WHEN CHARINDEX(N'' '', LTRIM(RTRIM(ISNULL([FullName], N'''')))) > 0
                            THEN LEFT(LTRIM(RTRIM(ISNULL([FullName], N''''))), LEN(LTRIM(RTRIM(ISNULL([FullName], N'''')))) - CHARINDEX(N'' '', REVERSE(LTRIM(RTRIM(ISNULL([FullName], N''''))))) )
                        ELSE LTRIM(RTRIM(ISNULL([FullName], N'''')))
                    END
                ELSE [FirstName]
            END,
            [LastName] = CASE
                WHEN ISNULL(LTRIM(RTRIM([LastName])), N'''') = N'''' THEN
                    CASE
                        WHEN CHARINDEX(N'' '', LTRIM(RTRIM(ISNULL([FullName], N'''')))) > 0
                            THEN RIGHT(LTRIM(RTRIM(ISNULL([FullName], N''''))), CHARINDEX(N'' '', REVERSE(LTRIM(RTRIM(ISNULL([FullName], N'''')))))-1)
                        ELSE N''''
                    END
                ELSE [LastName]
            END,
            [Mobile] = CASE WHEN ISNULL([Mobile], N'''') = N'''' THEN ISNULL([Phone], N'''') ELSE [Mobile] END,
            [FatherMobile] = CASE WHEN ISNULL([FatherMobile], N'''') = N'''' THEN ISNULL([FatherContact], N'''') ELSE [FatherMobile] END,
            [MotherMobile] = CASE WHEN ISNULL([MotherMobile], N'''') = N'''' THEN ISNULL([MotherContact], N'''') ELSE [MotherMobile] END;
    ');
END");

            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'FullName') IS NOT NULL ALTER TABLE [Students] DROP COLUMN [FullName];");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'Phone') IS NOT NULL ALTER TABLE [Students] DROP COLUMN [Phone];");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'FatherContact') IS NOT NULL ALTER TABLE [Students] DROP COLUMN [FatherContact];");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'MotherContact') IS NOT NULL ALTER TABLE [Students] DROP COLUMN [MotherContact];");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'FullName') IS NULL ALTER TABLE [Students] ADD [FullName] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_FullName] DEFAULT N'';");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'Phone') IS NULL ALTER TABLE [Students] ADD [Phone] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_Phone] DEFAULT N'';");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'FatherContact') IS NULL ALTER TABLE [Students] ADD [FatherContact] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_FatherContact] DEFAULT N'';");
            migrationBuilder.Sql(@"IF COL_LENGTH('Students', 'MotherContact') IS NULL ALTER TABLE [Students] ADD [MotherContact] nvarchar(max) NOT NULL CONSTRAINT [DF_Students_MotherContact] DEFAULT N'';");

            migrationBuilder.Sql(@"
UPDATE [Students]
SET [FullName] = LTRIM(RTRIM(CONCAT(ISNULL([FirstName], N''), N' ', ISNULL([LastName], N'')))),
    [Phone] = ISNULL([Mobile], N''),
    [FatherContact] = ISNULL([FatherMobile], N''),
    [MotherContact] = ISNULL([MotherMobile], N'');");
        }
    }
}
