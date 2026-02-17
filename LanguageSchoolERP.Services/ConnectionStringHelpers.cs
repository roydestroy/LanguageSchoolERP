using Microsoft.Data.SqlClient;

namespace LanguageSchoolERP.Services;

public static class ConnectionStringHelpers
{
    public static string EnsureRemoteDatabaseName(string localDatabaseName)
    {
        if (string.IsNullOrWhiteSpace(localDatabaseName))
        {
            throw new ArgumentException("Local database name is required.", nameof(localDatabaseName));
        }

        return localDatabaseName.EndsWith("_View", StringComparison.OrdinalIgnoreCase)
            ? localDatabaseName
            : $"{localDatabaseName}_View";
    }

    public static string EnsureLocalDatabaseName(string remoteDatabaseName)
    {
        if (string.IsNullOrWhiteSpace(remoteDatabaseName))
        {
            throw new ArgumentException("Remote database name is required.", nameof(remoteDatabaseName));
        }

        return remoteDatabaseName.EndsWith("_View", StringComparison.OrdinalIgnoreCase)
            ? remoteDatabaseName[..^5]
            : remoteDatabaseName;
    }

    public static string ReplaceDatabase(string connectionString, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException("Database name is required.", nameof(databaseName));
        }

        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName
        };

        return builder.ConnectionString;
    }
}
