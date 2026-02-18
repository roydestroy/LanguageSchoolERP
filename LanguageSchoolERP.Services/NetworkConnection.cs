using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LanguageSchoolERP.Services;

public sealed class NetworkConnection : IDisposable
{
    private readonly string _networkName;

    private const int RESOURCETYPE_DISK = 0x00000001;
    private const int CONNECT_TEMPORARY = 0x00000004;

    // Common SMB error codes
    private const int ERROR_SESSION_CREDENTIAL_CONFLICT = 1219;

    public NetworkConnection(string networkName, string username, string password)
    {
        _networkName = networkName;

        var nr = new NETRESOURCE
        {
            dwType = RESOURCETYPE_DISK,
            lpRemoteName = networkName
        };

        var result = WNetAddConnection2(ref nr, password, username, CONNECT_TEMPORARY);

        // If Windows already has a session to that server with different creds, retry once after disconnect
        if (result == ERROR_SESSION_CREDENTIAL_CONFLICT)
        {
            WNetCancelConnection2(networkName, 0, true);
            result = WNetAddConnection2(ref nr, password, username, CONNECT_TEMPORARY);
        }

        if (result != 0)
            throw new Win32Exception(result, $"Failed to connect to network share '{networkName}' as '{username}'.");
    }

    public void Dispose()
    {
        WNetCancelConnection2(_networkName, 0, true);
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2(
        ref NETRESOURCE netResource,
        string? password,
        string? username,
        int flags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2(
        string name,
        int flags,
        bool force);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NETRESOURCE
    {
        public int dwScope;
        public int dwType;
        public int dwDisplayType;
        public int dwUsage;
        public string? lpLocalName;
        public string? lpRemoteName;
        public string? lpComment;
        public string? lpProvider;
    }
}
