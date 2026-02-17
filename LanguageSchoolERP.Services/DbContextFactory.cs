using LanguageSchoolERP.Data;
using Microsoft.EntityFrameworkCore;

namespace LanguageSchoolERP.Services;

public class DbContextFactory
{
    private readonly AppState _state;
    private readonly DatabaseAppSettingsProvider _settingsProvider;

    public DbContextFactory(AppState state, DatabaseAppSettingsProvider settingsProvider)
    {
        _state = state;
        _settingsProvider = settingsProvider;
    }

    public SchoolDbContext Create()
    {
        var settings = _settingsProvider.Settings;
        var connectionString = _state.SelectedDatabaseMode == DatabaseMode.Local
            ? $"Server={settings.Local.Server};Database={settings.Local.Database};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;"
            : $"Server={settings.Remote.Server};Database={_state.SelectedDatabaseName};User Id=erp_viewer;Password=Th3redeemerz!;TrustServerCertificate=True;Encrypt=True;";

        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SchoolDbContext(options);
    }
}
