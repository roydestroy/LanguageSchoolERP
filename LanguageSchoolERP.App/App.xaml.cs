using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using LanguageSchoolERP.App.ViewModels;
using LanguageSchoolERP.App.Views;
using LanguageSchoolERP.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace LanguageSchoolERP.App;

public partial class App : Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        // Views / ViewModels
        services.AddSingleton<StudentsViewModel>();
        services.AddSingleton<StudentsView>();

        services.AddTransient<ProgramsListViewModel>();
        services.AddTransient<ProgramsView>();
        services.AddTransient<ProgramEditViewModel>();
        services.AddTransient<Windows.ProgramEditWindow>();

        services.AddSingleton<AcademicYearsViewModel>();
        services.AddSingleton<AcademicYearsView>();

        services.AddTransient<NewStudentViewModel>();
        services.AddTransient<Windows.NewStudentWindow>();

        services.AddTransient<StudentProfileViewModel>();
        services.AddTransient<Windows.StudentProfileWindow>();

        services.AddTransient<AddPaymentViewModel>();
        services.AddTransient<Windows.AddPaymentWindow>();

        services.AddTransient<AddProgramEnrollmentViewModel>();
        services.AddTransient<Windows.AddProgramEnrollmentWindow>();

        services.AddTransient<AddContractViewModel>();
        services.AddTransient<Windows.AddContractWindow>();

        services.AddTransient<Windows.StartupDatabaseOptionsWindow>();

        services.AddTransient<DatabaseImportViewModel>();
        services.AddTransient<DatabaseImportView>();

        // Services
        services.AddSingleton<ReceiptNumberService>();
        services.AddSingleton<ExcelReceiptGenerator>();
        services.AddSingleton<ContractDocumentService>();
        services.AddSingleton<ContractBookmarkBuilder>();
        services.AddTransient<IProgramService, ProgramService>();
        services.AddTransient<IDatabaseImportService, DatabaseImportService>();

        services.AddSingleton<DatabaseAppSettingsProvider>();
        services.AddSingleton<AppState>();
        services.AddSingleton<IGitHubUpdateService, GitHubUpdateService>();
        services.AddSingleton<DbContextFactory>();

        // Main window
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Fire-and-forget startup check (silent if not user-initiated)
        _ = CheckForUpdatesInteractiveAsync(mainWindow, userInitiated: false);

        base.OnStartup(e);
    }

    /// <summary>
    /// Interactive update check (used both on startup and from menu "Βοήθεια → Έλεγχος ενημερώσεων").
    /// - On startup (userInitiated=false): only prompts if update is available; errors are logged silently.
    /// - When user initiated: shows "no updates" and error messages.
    /// </summary>
    public static async Task CheckForUpdatesInteractiveAsync(Window owner, bool userInitiated)
    {
        try
        {
            UpdaterLog.Write("App", "Checking for updates.");

            var settingsProvider = Services.GetRequiredService<DatabaseAppSettingsProvider>();
            var updateService = Services.GetRequiredService<IGitHubUpdateService>();

            var result = await updateService.CheckForUpdateAsync();

            if (!result.IsEnabled)
            {
                UpdaterLog.Write("App", "Update checks are disabled.");
                if (userInitiated)
                {
                    MessageBox.Show(owner,
                        "Οι ενημερώσεις είναι απενεργοποιημένες.",
                        "Έλεγχος ενημερώσεων",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                UpdaterLog.Write("App", $"Update check failed: {result.Error}");
                if (userInitiated)
                {
                    MessageBox.Show(owner,
                        $"Αποτυχία ελέγχου ενημερώσεων:\n{result.Error}",
                        "Έλεγχος ενημερώσεων",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                return;
            }

            if (!result.IsUpdateAvailable)
            {
                UpdaterLog.Write("App", "No update available.");
                if (userInitiated)
                {
                    MessageBox.Show(owner,
                        "Δεν υπάρχει νεότερη έκδοση.",
                        "Έλεγχος ενημερώσεων",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(result.AssetDownloadUrl))
            {
                UpdaterLog.Write("App", "Update available but asset download URL is missing.");
                if (userInitiated)
                {
                    MessageBox.Show(owner,
                        "Βρέθηκε νέα έκδοση, αλλά λείπει το αρχείο ενημέρωσης (asset).",
                        "Διαθέσιμη ενημέρωση",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                return;
            }

            var releaseName = !string.IsNullOrWhiteSpace(result.ReleaseName)
                ? result.ReleaseName
                : (result.ReleaseTag ?? result.LatestVersion?.ToString() ?? "latest");

            var message =
                $"Βρέθηκε νέα έκδοση ({releaseName}).\n" +
                $"Τρέχουσα: {result.CurrentVersion}\n" +
                $"Διαθέσιμη: {result.LatestVersion}\n\n" +
                "Θέλετε να γίνει λήψη και εγκατάσταση τώρα;";

            var choice = MessageBox.Show(owner,
                message,
                "Διαθέσιμη ενημέρωση",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (choice != MessageBoxResult.Yes)
            {
                UpdaterLog.Write("App", "User chose not to update now.");
                return;
            }

            await DownloadAndRunUpdaterAsync(result, owner);

            // Close the app so the updater can replace files
            UpdaterLog.Write("App", "Shutting down app for update.");
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdaterLog.Write("App", "Unexpected update flow error.", ex);

            // If this was just a startup silent check, do not annoy the user
            if (userInitiated)
            {
                MessageBox.Show(owner,
                    "Προέκυψε σφάλμα κατά την ενημέρωση. Δοκιμάστε ξανά αργότερα.",
                    "Ενημέρωση",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private static async Task DownloadAndRunUpdaterAsync(UpdateCheckResult result, Window owner)
    {
        var settingsProvider = Services.GetRequiredService<DatabaseAppSettingsProvider>();
        var settings = settingsProvider.Settings.Update;

        // Use the version as a folder name (stable and readable)
        var versionFolder = result.LatestVersion?.ToString() ?? result.ReleaseTag ?? "latest";

        var updateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LanguageSchoolERP",
            "Updates",
            versionFolder);

        Directory.CreateDirectory(updateDir);

        var zipPath = Path.Combine(updateDir, $"update_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");
        UpdaterLog.Write("App", $"Downloading update asset to '{zipPath}' from '{result.AssetDownloadUrl}'.");

        await DownloadFileWithProgressAsync(result.AssetDownloadUrl!, zipPath, owner);


        // Prefer the updater located next to the running app
        var updaterExe = ResolveUpdaterExePath(settings.InstallFolder);
        if (!File.Exists(updaterExe))
            throw new FileNotFoundException($"Updater executable not found at '{updaterExe}'.");

        // Prefer restarting the exact exe we are currently running
        var appExeFullPath =
            Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.Combine(AppContext.BaseDirectory, "LanguageSchoolERP.App.exe");

        // IMPORTANT: Use the folder we are actually running from, not a configured value.
        // This keeps OTA deterministic (updates the current install).
        var installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

        var args =
            $"--pid {Environment.ProcessId} " +
            $"--zip \"{zipPath}\" " +
            $"--installDir \"{installDir}\" " +
            $"--exe \"{appExeFullPath}\"";

        UpdaterLog.Write("App", $"Starting updater '{updaterExe}' with args '{args}'.");

        Process.Start(new ProcessStartInfo
        {
            FileName = updaterExe,
            Arguments = args,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(updaterExe) ?? installDir
        });
    }
    private static async Task DownloadFileWithProgressAsync(
    string url,
    string destinationPath,
    Window owner,
    CancellationToken cancellationToken = default)
    {
        var dlg = new Windows.UpdateDownloadWindow { Owner = owner };
        dlg.Show();
        dlg.Activate();
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "LanguageSchoolERP-Updater");
            httpClient.Timeout = TimeSpan.FromMinutes(20);

            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength; // can be null
            long received = 0;

            dlg.Dispatcher.Invoke(() =>
            {
                if (totalBytes is null)
                    dlg.SetIndeterminate("Λήψη... (άγνωστο μέγεθος)");
                else
                    dlg.SetProgress(0, $"0 / {FormatBytes(totalBytes.Value)}");
            });

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);

            // overwrite if it exists
            await using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[1024 * 64]; // 64KB
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                received += read;

                if (totalBytes is not null && totalBytes.Value > 0)
                {
                    var percent = (received * 100d) / totalBytes.Value;
                    var details = $"{FormatBytes(received)} / {FormatBytes(totalBytes.Value)}";
                    dlg.Dispatcher.Invoke(() => dlg.SetProgress(percent, details));
                }
                else
                {
                    var details = $"{FormatBytes(received)} λήφθηκαν...";
                    dlg.Dispatcher.Invoke(() => dlg.SetIndeterminate(details));
                }
            }

            dlg.Dispatcher.Invoke(() =>
            {
                dlg.SetProgress(100, "Ολοκληρώθηκε η λήψη.");
            });
        }
        finally
        {
            dlg.Close();
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static string ResolveUpdaterExePath(string installFolder)
    {
        // First choice: updater next to the running app
        var localPath = Path.Combine(AppContext.BaseDirectory, "LanguageSchoolERP.Updater.exe");
        if (File.Exists(localPath))
            return localPath;

        // Fallback: configured install folder
        if (string.IsNullOrWhiteSpace(installFolder))
            return localPath;

        return Path.Combine(installFolder, "LanguageSchoolERP.Updater.exe");
    }
}
