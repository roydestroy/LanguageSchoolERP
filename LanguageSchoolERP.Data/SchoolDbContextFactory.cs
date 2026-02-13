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
            @"Server=.\SQLEXPRESS;Database=FilotheiSchoolERP;Trusted_Connection=True;TrustServerCertificate=True");

        return new SchoolDbContext(optionsBuilder.Options);
    }
}
