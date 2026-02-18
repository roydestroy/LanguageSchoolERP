using LanguageSchoolERP.Core.Configuration;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text.Json;

namespace LanguageSchoolERP.Services;


public sealed class DatabaseAppSettingsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const string SettingsFolder = @"C:\ProgramData\LanguageSchoolERP";
    private const string SettingsFile = "appsettings.json";
    private readonly string _fullPath = Path.Combine(SettingsFolder, SettingsFile);

    public DatabaseAppSettingsProvider()
    {
        Settings = LoadOrCreate();
    }

    public DatabaseAppSettings Settings { get; }

    public IReadOnlyList<RemoteDatabaseOption> RemoteDatabases => Settings.Remote.Databases;

    public void Save()
    {
        Normalize(Settings);
        File.WriteAllText(_fullPath, JsonSerializer.Serialize(Settings, JsonOptions));
    }

    private static DatabaseAppSettings LoadOrCreate()
    {
        Directory.CreateDirectory(SettingsFolder);
        var fullPath = Path.Combine(SettingsFolder, SettingsFile);

        if (!File.Exists(fullPath))
        {
            var defaults = CreateDefaults();
            File.WriteAllText(fullPath, JsonSerializer.Serialize(defaults, JsonOptions));
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(fullPath);
            var parsed = JsonSerializer.Deserialize<DatabaseAppSettings>(json) ?? CreateDefaults();
            Normalize(parsed);
            return parsed;
        }
        catch
        {
            var defaults = CreateDefaults();
            File.WriteAllText(fullPath, JsonSerializer.Serialize(defaults, JsonOptions));
            return defaults;
        }
    }

    private static DatabaseAppSettings CreateDefaults() => new()
    {
        Local = new LocalDatabaseSettings
        {
            Server = @".\SQLEXPRESS",
            Database = "FilotheiSchoolERP",
            ConnectionString = "Server=.\\SQLEXPRESS;Database=FilotheiSchoolERP;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;"
        },
        Remote = new RemoteDatabaseSettings
        {
            Server = "100.104.49.73,1433",
            User = "erp_viewer",
            Password = "Th3redeemerz!",
            ConnectionString = "", // let Normalize() build it
            Databases =
            [
                new RemoteDatabaseOption { Key = "Filothei", Database = "FilotheiSchoolERP_View" },
                new RemoteDatabaseOption { Key = "Nea Ionia", Database = "NeaIoniaSchoolERP_View" }
            ]
        }
,
        Startup = new StartupDatabaseSettings
        {
            Mode = DatabaseMode.Local,
            LocalDatabase = "FilotheiSchoolERP",
            RemoteDatabase = "FilotheiSchoolERP_View"
        },
        Update = new UpdateSettings
        {
            Enabled = true,
            GitHubOwner = "roydestroy",
            GitHubRepo = "LanguageSchoolERP",
            IncludePrerelease = false,
            AssetName = "LanguageSchoolERP-win-x64.zip",
            InstallFolder = @"C:\Apps\LanguageSchoolERP"
        },
        Backup = new BackupSettings()
    };

    private static void Normalize(DatabaseAppSettings settings)
    {
        settings.Local ??= new LocalDatabaseSettings();
        settings.Remote ??= new RemoteDatabaseSettings();
        settings.Startup ??= new StartupDatabaseSettings();
        settings.Update ??= new UpdateSettings();
        settings.Backup ??= new BackupSettings();


        if (string.IsNullOrWhiteSpace(settings.Local.Server))
            settings.Local.Server = @".\SQLEXPRESS";

        if (string.IsNullOrWhiteSpace(settings.Local.Database))
            settings.Local.Database = "FilotheiSchoolERP";

        if (string.IsNullOrWhiteSpace(settings.Remote.Server))
            settings.Remote.Server = "100.104.49.73,1433";

        settings.Remote.Databases ??= [];
        if (settings.Remote.Databases.Count == 0)
        {
            settings.Remote.Databases.Add(new RemoteDatabaseOption { Key = "Filothei", Database = "FilotheiSchoolERP_View" });
            settings.Remote.Databases.Add(new RemoteDatabaseOption { Key = "NeaIonia", Database = "NeaIoniaSchoolERP_View" });
        }

        foreach (var db in settings.Remote.Databases)
        {
            if (string.IsNullOrWhiteSpace(db.Key))
                db.Key = db.Database;
        }

        if (string.IsNullOrWhiteSpace(settings.Local.ConnectionString))
        {
            settings.Local.ConnectionString = BuildTrustedConnectionString(settings.Local.Server, settings.Local.Database);
        }
        else
        {
            settings.Local.ConnectionString = EnsureDatabaseInConnectionString(settings.Local.ConnectionString, settings.Local.Database);
        }

        if (string.IsNullOrWhiteSpace(settings.Remote.ConnectionString))
        {
            var firstRemoteDb = settings.Remote.Databases.First().Database;
            settings.Remote.ConnectionString = BuildRemoteConnectionString(settings.Remote, firstRemoteDb);
        }

        if (string.IsNullOrWhiteSpace(settings.Startup.LocalDatabase))
            settings.Startup.LocalDatabase = settings.Local.Database;

        if (string.IsNullOrWhiteSpace(settings.Startup.RemoteDatabase))
            settings.Startup.RemoteDatabase = settings.Remote.Databases.First().Database;

        if (string.IsNullOrWhiteSpace(settings.Update.GitHubOwner))
            settings.Update.GitHubOwner = "roydestroy";

        if (string.IsNullOrWhiteSpace(settings.Update.GitHubRepo))
            settings.Update.GitHubRepo = "LanguageSchoolERP";

        if (string.IsNullOrWhiteSpace(settings.Update.AssetName))
            settings.Update.AssetName = "LanguageSchoolERP-win-x64.zip";

        if (string.IsNullOrWhiteSpace(settings.Update.InstallFolder))
            settings.Update.InstallFolder = @"C:\Apps\LanguageSchoolERP";
    }

    private static string BuildTrustedConnectionString(string server, string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Encrypt = true
        };

        return builder.ConnectionString;
    }

    private static string BuildRemoteConnectionString(RemoteDatabaseSettings remoteSettings, string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = remoteSettings.Server,
            InitialCatalog = database,
            TrustServerCertificate = true,
            Encrypt = true
        };

        if (!string.IsNullOrWhiteSpace(remoteSettings.User))
        {
            builder.UserID = remoteSettings.User;
            builder.Password = remoteSettings.Password ?? string.Empty;
            builder.IntegratedSecurity = false;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        return builder.ConnectionString;
    }

    private static string EnsureDatabaseInConnectionString(string connectionString, string database)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = database
        };

        return builder.ConnectionString;
    }
}

public sealed class DatabaseAppSettings
{
    public LocalDatabaseSettings Local { get; set; } = new();
    public RemoteDatabaseSettings Remote { get; set; } = new();
    public StartupDatabaseSettings Startup { get; set; } = new();
    public UpdateSettings Update { get; set; } = new();
    public BackupSettings Backup { get; set; } = new();
}

public sealed class LocalDatabaseSettings
{
    public string Server { get; set; } = @".\SQLEXPRESS";
    public string Database { get; set; } = "FilotheiSchoolERP";
    public string ConnectionString { get; set; } = "";
}

public sealed class RemoteDatabaseSettings
{
    public string Server { get; set; } = "100.104.49.73,1433";
    public string ConnectionString { get; set; } = "";
    public string? User { get; set; }
    public string? Password { get; set; }
    public List<RemoteDatabaseOption> Databases { get; set; } = [];
}

public sealed class StartupDatabaseSettings
{
    public DatabaseMode Mode { get; set; } = DatabaseMode.Local;
    public string LocalDatabase { get; set; } = "FilotheiSchoolERP";
    public string RemoteDatabase { get; set; } = "FilotheiSchoolERP_View";
}

public sealed class RemoteDatabaseOption
{
    public string Key { get; set; } = "Filothei";
    public string Database { get; set; } = "FilotheiSchoolERP_View";
}

public sealed class UpdateSettings
{
    public bool Enabled { get; set; } = true;
    public string GitHubOwner { get; set; } = "roydestroy";
    public string GitHubRepo { get; set; } = "LanguageSchoolERP";
    public bool IncludePrerelease { get; set; }
    public string AssetName { get; set; } = "LanguageSchoolERP-win-x64.zip";
    public string InstallFolder { get; set; } = @"C:\Apps\LanguageSchoolERP";
}
