using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LanguageSchoolERP.Data;

public class SchoolDbContextFactory : IDesignTimeDbContextFactory<SchoolDbContext>
{
    private static readonly string[] SettingsPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LanguageSchoolERP", "appsettings.json"),
        @"C:\ProgramData\LanguageSchoolERP\appsettings.json"
    ];

    public SchoolDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SchoolDbContext>();

        var (server, database) = LoadLocalSettings();
        optionsBuilder.UseSqlServer(
            $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;");

        return new SchoolDbContext(optionsBuilder.Options);
    }

    private static (string Server, string Database) LoadLocalSettings()
    {
        const string defaultServer = @".\SQLEXPRESS";
        const string defaultDatabase = "FilotheiSchoolERP";

        var settingsPath = SettingsPaths.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(settingsPath))
            return (defaultServer, defaultDatabase);

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var local = doc.RootElement.TryGetProperty("Local", out var localNode) ? localNode : default;

            var server = local.ValueKind != JsonValueKind.Undefined && local.TryGetProperty("Server", out var serverNode)
                ? serverNode.GetString()
                : defaultServer;

            var database = local.ValueKind != JsonValueKind.Undefined && local.TryGetProperty("Database", out var dbNode)
                ? dbNode.GetString()
                : defaultDatabase;

            return (
                string.IsNullOrWhiteSpace(server) ? defaultServer : server,
                string.IsNullOrWhiteSpace(database) ? defaultDatabase : database);
        }
        catch
        {
            return (defaultServer, defaultDatabase);
        }
    }
}
