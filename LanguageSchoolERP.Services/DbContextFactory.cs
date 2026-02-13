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
            .UseSqlServer($@"Server=.\SQLEXPRESS;Database={_state.SelectedDatabaseName};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new SchoolDbContext(options);
    }
}
