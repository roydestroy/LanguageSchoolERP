using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace LanguageSchoolERP.Services;

public interface IGitHubUpdateService
{
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}

public sealed class GitHubUpdateService : IGitHubUpdateService
{
    private readonly DatabaseAppSettingsProvider _settingsProvider;

    public GitHubUpdateService(DatabaseAppSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = _settingsProvider.Settings.Update;
            if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.GitHubOwner) || string.IsNullOrWhiteSpace(settings.GitHubRepo))
                return UpdateCheckResult.Disabled();

            var currentVersion = GetCurrentVersion();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "LanguageSchoolERP-Updater");

            var requestUri = settings.IncludePrerelease
                ? $"https://api.github.com/repos/{settings.GitHubOwner}/{settings.GitHubRepo}/releases"
                : $"https://api.github.com/repos/{settings.GitHubOwner}/{settings.GitHubRepo}/releases/latest";

            using var response = await httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return UpdateCheckResult.Failed($"GitHub API returned {(int)response.StatusCode}.");

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = settings.IncludePrerelease
                ? ParseFirstRelease(payload)
                : ParseRelease(payload);

            if (release is null)
                return UpdateCheckResult.Failed("Unable to parse release information from GitHub.");

            if (!TryParseVersion(release.TagName, out var latestVersion))
                return UpdateCheckResult.Failed($"Latest release tag '{release.TagName}' is not a valid version.");

            if (latestVersion <= currentVersion)
                return UpdateCheckResult.UpToDate(currentVersion, latestVersion);

            if (string.IsNullOrWhiteSpace(release.AssetDownloadUrl))
                return UpdateCheckResult.Failed($"Release '{release.TagName}' does not contain required asset '{settings.AssetName}'.");

            return UpdateCheckResult.UpdateAvailable(
                currentVersion,
                latestVersion,
                release.HtmlUrl,
                release.Name,
                release.TagName,
                release.AssetDownloadUrl);
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed(ex.Message);
        }
    }

    private static Version GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly.GetName().Version ?? new Version(1, 0, 0, 0);
    }

    private ReleaseInfo? ParseFirstRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            return null;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("draft", out var draftEl) && draftEl.GetBoolean())
                continue;

            var release = ParseReleaseElement(item);
            if (release is not null)
                return release;
        }

        return null;
    }

    private ReleaseInfo? ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ParseReleaseElement(doc.RootElement);
    }

    private ReleaseInfo? ParseReleaseElement(JsonElement element)
    {
        if (!element.TryGetProperty("tag_name", out var tagNameEl) ||
            !element.TryGetProperty("html_url", out var htmlUrlEl))
            return null;

        var tagName = tagNameEl.GetString();
        var htmlUrl = htmlUrlEl.GetString();
        if (string.IsNullOrWhiteSpace(tagName) || string.IsNullOrWhiteSpace(htmlUrl))
            return null;

        var name = element.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        var assetUrl = FindAssetDownloadUrl(element, _settingsProvider.Settings.Update.AssetName);

        return new ReleaseInfo(tagName, htmlUrl, name, assetUrl);
    }

    private static string? FindAssetDownloadUrl(JsonElement releaseElement, string desiredAssetName)
    {
        if (!releaseElement.TryGetProperty("assets", out var assetsEl) || assetsEl.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var assetEl in assetsEl.EnumerateArray())
        {
            if (!assetEl.TryGetProperty("name", out var nameEl) ||
                !assetEl.TryGetProperty("browser_download_url", out var urlEl))
                continue;

            var name = nameEl.GetString();
            var url = urlEl.GetString();

            if (string.Equals(name, desiredAssetName, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        return null;
    }

    private static bool TryParseVersion(string rawTag, out Version version)
    {
        var tag = rawTag.Trim();
        if (tag.StartsWith('v') || tag.StartsWith('V'))
            tag = tag[1..];

        return Version.TryParse(tag, out version!);
    }

    private sealed record ReleaseInfo(string TagName, string HtmlUrl, string? Name, string? AssetDownloadUrl);
}

public sealed record UpdateCheckResult(
    bool IsEnabled,
    bool IsUpdateAvailable,
    Version? CurrentVersion,
    Version? LatestVersion,
    string? ReleaseUrl,
    string? ReleaseName,
    string? ReleaseTag,
    string? AssetDownloadUrl,
    string? Error)
{
    public static UpdateCheckResult Disabled() => new(false, false, null, null, null, null, null, null, null);

    public static UpdateCheckResult Failed(string error) => new(true, false, null, null, null, null, null, null, error);

    public static UpdateCheckResult UpToDate(Version currentVersion, Version latestVersion)
        => new(true, false, currentVersion, latestVersion, null, null, null, null, null);

    public static UpdateCheckResult UpdateAvailable(
        Version currentVersion,
        Version latestVersion,
        string releaseUrl,
        string? releaseName,
        string releaseTag,
        string assetDownloadUrl)
        => new(true, true, currentVersion, latestVersion, releaseUrl, releaseName, releaseTag, assetDownloadUrl, null);
}
