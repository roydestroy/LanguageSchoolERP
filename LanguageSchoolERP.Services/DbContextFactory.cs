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
        var baseConnectionString = _state.SelectedDatabaseMode == DatabaseMode.Local
            ? settings.Local.ConnectionString
            : settings.Remote.ConnectionString;

        var connectionString = ConnectionStringHelpers.ReplaceDatabase(baseConnectionString, _state.SelectedDatabaseName);

        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null))
            .Options;

        return new SchoolDbContext(options);
    }
}
