using System.ComponentModel;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;

namespace LanguageSchoolERP.Services;

public static class RemoteConnectivityDiagnostics
{
    private const int CredentialConflictErrorCode = 1219;

    public static async Task<ConnectivityCheckResult> CheckRemoteSqlAsync(string connectionString, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ConnectivityCheckResult.Fail("Tailscale disconnected", "Remote SQL connection string is missing.");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (Exception ex)
        {
            return ConnectivityCheckResult.Fail("Tailscale disconnected", $"Invalid remote SQL connection string: {ex.Message}");
        }

        try
        {
            builder.ConnectTimeout = 3;
            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync(cancellationToken);
            return ConnectivityCheckResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ConnectivityCheckResult.Fail("Tailscale disconnected", "Connectivity check was cancelled.");
        }
        catch (SqlException ex) when (IsConnectivitySqlException(ex))
        {
            return ConnectivityCheckResult.Fail("Tailscale disconnected", ex.Message);
        }
        catch (Exception ex)
        {
            return ConnectivityCheckResult.Fail("Tailscale disconnected", ex.Message);
        }
    }

    public static ConnectivityCheckResult CheckRemoteShare(string sharePath, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(sharePath))
        {
            return ConnectivityCheckResult.Fail("remote share unavailable", "Remote share path is missing.");
        }

        try
        {
            using var connection = new NetworkConnection(sharePath, username, password);

            if (!Directory.Exists(sharePath))
            {
                return ConnectivityCheckResult.Fail("remote share unavailable", $"Remote share path '{sharePath}' is not accessible.");
            }

            return ConnectivityCheckResult.Success();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == CredentialConflictErrorCode)
        {
            return ConnectivityCheckResult.Fail("credential conflict", ex.Message);
        }
        catch (Win32Exception ex)
        {
            return ConnectivityCheckResult.Fail("remote share unavailable", ex.Message);
        }
        catch (IOException ex)
        {
            return ConnectivityCheckResult.Fail("remote share unavailable", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ConnectivityCheckResult.Fail("remote share unavailable", ex.Message);
        }
    }

    private static bool IsConnectivitySqlException(SqlException exception)
    {
        return exception.Errors.Cast<SqlError>().Any(error =>
            error.Number is -2 or 53 or 11001 or 4060 || error.Class >= 20);
    }

}

public sealed record ConnectivityCheckResult(bool IsSuccess, string? UserMessage, string? Details)
{
    public static ConnectivityCheckResult Success() => new(true, null, null);

    public static ConnectivityCheckResult Fail(string userMessage, string? details = null) => new(false, userMessage, details);
}
