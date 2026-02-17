using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using LanguageSchoolERP.App.ViewModels;
using LanguageSchoolERP.App.Views;
using LanguageSchoolERP.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LanguageSchoolERP.App;

public partial class App : Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

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

        services.AddSingleton<ReceiptNumberService>();
        services.AddSingleton<ExcelReceiptGenerator>();
        services.AddSingleton<ContractDocumentService>();
        services.AddSingleton<ContractBookmarkBuilder>();
        services.AddTransient<IProgramService, ProgramService>();

        services.AddSingleton<DatabaseAppSettingsProvider>();
        services.AddSingleton<AppState>();
        services.AddSingleton<IGitHubUpdateService, GitHubUpdateService>();

        services.AddSingleton<DbContextFactory>();

        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        _ = CheckForUpdatesInteractiveAsync(mainWindow, userInitiated: false);

        base.OnStartup(e);
    }

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
                if (userInitiated)
                    MessageBox.Show(owner, "Οι ενημερώσεις είναι απενεργοποιημένες.", "Έλεγχος ενημερώσεων", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.Error))
            {
                UpdaterLog.Write("App", $"Update check failed: {result.Error}");
                if (userInitiated)
                    MessageBox.Show(owner, $"Αποτυχία ελέγχου ενημερώσεων:\n{result.Error}", "Έλεγχος ενημερώσεων", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!result.IsUpdateAvailable || string.IsNullOrWhiteSpace(result.AssetDownloadUrl))
            {
                if (userInitiated)
                    MessageBox.Show(owner, "Δεν υπάρχει νεότερη έκδοση.", "Έλεγχος ενημερώσεων", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var releaseName = string.IsNullOrWhiteSpace(result.ReleaseName)
                ? result.ReleaseTag
                : result.ReleaseName;

            var message =
                $"Βρέθηκε νέα έκδοση ({releaseName}).\n" +
                $"Τρέχουσα: {result.CurrentVersion}\n" +
                $"Διαθέσιμη: {result.LatestVersion}\n\n" +
                "Θέλετε να γίνει λήψη και εγκατάσταση τώρα;";

            var choice = MessageBox.Show(owner, message, "Διαθέσιμη ενημέρωση", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (choice != MessageBoxResult.Yes)
                return;

            await DownloadAndRunUpdaterAsync(result, settingsProvider.Settings.Update);
            Shutdown();
        }
        catch (Exception ex)
        {
            UpdaterLog.Write("App", "Unexpected update flow error.", ex);
            MessageBox.Show(owner,
                "Προέκυψε σφάλμα κατά την ενημέρωση. Δοκιμάστε ξανά αργότερα.",
                "Ενημέρωση",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static async Task DownloadAndRunUpdaterAsync(UpdateCheckResult result, UpdateSettings settings)
    {
        var versionFolder = result.LatestVersion?.ToString() ?? result.ReleaseTag ?? "latest";
        var updateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LanguageSchoolERP",
            "Updates",
            versionFolder);
        Directory.CreateDirectory(updateDir);

        var zipPath = Path.Combine(updateDir, "update.zip");
        UpdaterLog.Write("App", $"Downloading update asset to '{zipPath}'.");

        using (var httpClient = new HttpClient())
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", "LanguageSchoolERP-Updater");
            using var response = await httpClient.GetAsync(result.AssetDownloadUrl!, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync();
            await using var target = File.Create(zipPath);
            await source.CopyToAsync(target);
        }

        var updaterExe = ResolveUpdaterExePath(settings.InstallFolder);
        if (!File.Exists(updaterExe))
            throw new FileNotFoundException($"Updater executable not found at '{updaterExe}'.");

        var appExe = Process.GetCurrentProcess().MainModule?.FileName ?? "LanguageSchoolERP.App.exe";
        var appExeName = Path.GetFileName(appExe);
        var installDir = string.IsNullOrWhiteSpace(settings.InstallFolder)
            ? AppContext.BaseDirectory
            : settings.InstallFolder;

        var args = $"--pid {Environment.ProcessId} --zip \"{zipPath}\" --installDir \"{installDir}\" --exe \"{appExeName}\"";

        UpdaterLog.Write("App", $"Starting updater '{updaterExe}' with args '{args}'.");

        Process.Start(new ProcessStartInfo
        {
            FileName = updaterExe,
            Arguments = args,
            UseShellExecute = false
        });
    }

    private static string ResolveUpdaterExePath(string installFolder)
    {
        var localPath = Path.Combine(AppContext.BaseDirectory, "LanguageSchoolERP.Updater.exe");
        if (File.Exists(localPath))
            return localPath;

        return Path.Combine(installFolder, "LanguageSchoolERP.Updater.exe");
    }
}
