using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LanguageSchoolERP.Data;

public class SchoolDbContextFactory : IDesignTimeDbContextFactory<SchoolDbContext>
{
    public SchoolDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SchoolDbContext>();

        // Same connection string as the app (for now)
        optionsBuilder.UseSqlServer(
            @"Server=100.104.49.73,1433;Database=FilotheiSchoolERP;User Id=erp_app;Password=Th3redeemerz!;TrustServerCertificate=True;Encrypt=True;");

        return new SchoolDbContext(optionsBuilder.Options);
    }
}
