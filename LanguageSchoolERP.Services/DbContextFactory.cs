using LanguageSchoolERP.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageSchoolERP.Services;

public class DbContextFactory
{
    private readonly AppState _state;

    public DbContextFactory(AppState state)
    {
        _state = state;
    }

    public SchoolDbContext Create()
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer($"Server=100.104.49.73,1433;Database={_state.SelectedDatabaseName};User Id=erp_app;Password=Th3redeemerz!;TrustServerCertificate=True;Encrypt=True;")
            .Options;

        return new SchoolDbContext(options);
    }
}
