using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using LanguageSchoolERP.App.ViewModels;
using LanguageSchoolERP.App.Views;
using LanguageSchoolERP.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using Microsoft.Data.Sql;
using Microsoft.EntityFrameworkCore;

namespace LanguageSchoolERP.App;

public partial class App : Application
{
    private const string DefaultLocalSqlServer = @".\SQLEXPRESS";
    private const string TailscaleDownloadUrl = "https://tailscale.com/download";
    private const string SqlExpressDownloadUrl = "https://www.microsoft.com/en-us/sql-server/sql-server-downloads";
    public static ServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        WindowStatePersistence.Register();

        // 1) Build DI first (same container for both modes)
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

        services.AddSingleton<StatisticsViewModel>();
        services.AddSingleton<StatisticsView>();

        services.AddTransient<DailyPaymentsViewModel>();
        services.AddTransient<DailyPaymentsView>();

        services.AddTransient<StudentContactsExportViewModel>();
        services.AddTransient<StudentContactsExportView>();

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
        services.AddTransient<Windows.LocalSqlServerPickerWindow>();

        services.AddTransient<DatabaseImportViewModel>();
        services.AddTransient<DatabaseImportView>();

        // Services
        services.AddSingleton<ReceiptNumberService>();
        services.AddSingleton<ExcelReceiptGenerator>();
        services.AddSingleton<ContractDocumentService>();
        services.AddSingleton<ContractBookmarkBuilder>();
        services.AddSingleton<DailyPaymentsReportService>();
        services.AddSingleton<StudentContactsExcelExportService>();
        services.AddTransient<IProgramService, ProgramService>();
        services.AddSingleton<IExcelImportRouter, FilenamePatternExcelImportRouter>();
        services.AddSingleton<IExcelWorkbookParser, ExcelInteropWorkbookParser>();
        services.AddTransient<IDatabaseImportService, DatabaseImportService>();
        services.AddTransient<IDatabaseCloneService, DatabaseCloneService>();

        services.AddSingleton<DatabaseAppSettingsProvider>();
        services.AddSingleton<AppState>();
        services.AddSingleton<IGitHubUpdateService, GitHubUpdateService>();
        services.AddSingleton<DbContextFactory>();

        // TODO: Add your backup services/runners when you create them, e.g.
        // services.AddSingleton<BackupTaskRunner>();
        // services.AddSingleton<BackupBootstrapper>();

        // Main window
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        // 2) Headless mode for Scheduled Task (NO UI)
        if (e.Args.Any(a => string.Equals(a, "--run-backup", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                // Prefer DI-resolved runner if you have it:
                // var runner = Services.GetRequiredService<BackupTaskRunner>();
                // var exitCode = await runner.RunAsync();

                // Or if your runner is static:
                var force = e.Args.Any(a => a.Equals("--force", StringComparison.OrdinalIgnoreCase));
                var exitCode = await BackupTaskRunner.RunAsync(force);
                Environment.Exit(exitCode);
            }
            catch (Exception ex)
            {
                // If you have logging, log here
                // UpdaterLog.Write("Backup", "Headless backup failed.", ex);
                Environment.Exit(2);
            }
            return;
        }

        var settingsProvider = Services.GetRequiredService<DatabaseAppSettingsProvider>();
        var appState = Services.GetRequiredService<AppState>();

        var resolvedLocalServer = await EnsureLocalServerSelectionAsync(settingsProvider);
        if (!string.IsNullOrWhiteSpace(resolvedLocalServer))
            settingsProvider.UpdateLocalServer(resolvedLocalServer);

        var tailscaleInstalled = IsTailscaleInstalled();
        if (!tailscaleInstalled)
        {
            appState.SetDatabaseImportEnabled(false);
            appState.SetRemoteModeEnabled(false);
        }

        var remoteConnectivity = tailscaleInstalled
            ? await CheckRemoteConnectivityAsync(settingsProvider.Settings, appState.SelectedRemoteDatabaseName)
            : ConnectivityCheckResult.Fail("Tailscale disconnected", "Tailscale is not installed.");

        appState.SetRemoteModeEnabled(remoteConnectivity.IsSuccess);

        if (tailscaleInstalled && !remoteConnectivity.IsSuccess)
        {
            appState.SetDatabaseImportEnabled(false);
        }

        var localAvailability = await CheckLocalDatabasesAvailabilityAsync(settingsProvider.Settings.Local.Server);
        appState.UpdateLocalDatabaseAvailability(localAvailability.HasFilothei, localAvailability.HasNeaIonia);

        var navigateToImportOnStartup = false;
        if (!localAvailability.HasAny)
        {
            if (remoteConnectivity.IsSuccess)
            {
                var choice = MessageBox.Show(
                    "Δεν βρέθηκε καμία τοπική βάση (Filothei/Nea Ionia).\nΘέλετε να μεταβείτε στις Ρυθμίσεις για εισαγωγή βάσης από remote;",
                    "Τοπική βάση",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                navigateToImportOnStartup = choice == MessageBoxResult.Yes;
            }
            else
            {
                MessageBox.Show(
                    "Δεν βρέθηκε τοπική βάση και η remote σύνδεση δεν είναι διαθέσιμη.\nΕγκαταστήστε/συνδεθείτε στο Tailscale και μετά κάντε εισαγωγή βάσης.",
                    "Βάση μη διαθέσιμη",
                    MessageBoxButton.OK,
                MessageBoxImage.Warning);
            }
        }

        if (localAvailability.HasAny)
        {
            var migrationOutcome = await TryAutoMigrateLocalDatabasesAsync(appState);
            if (!migrationOutcome.Success)
            {
                MessageBox.Show(
                    "Η εφαρμογή προσπάθησε να ενημερώσει αυτόματα τη δομή της τοπικής βάσης, αλλά κάτι πήγε στραβά.\n" +
                    "Για να συνεχίσετε με ασφάλεια, μεταβείτε στις Ρυθμίσεις και κάντε ξανά εισαγωγή της βάσης από το remote περιβάλλον.",
                    "Αποτυχία αυτόματης ενημέρωσης βάσης",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                navigateToImportOnStartup = true;
            }
            else if (migrationOutcome.UpdatedDatabases > 0)
            {
                MessageBox.Show(
                    $"Η ενημέρωση της βάσης ολοκληρώθηκε επιτυχώς ({migrationOutcome.UpdatedDatabases} βάση/βάσεις).\nΗ εφαρμογή είναι έτοιμη για χρήση.",
                    "Η ενημέρωση ολοκληρώθηκε",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // 3) Normal startup (UI)
        base.OnStartup(e);

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        if (navigateToImportOnStartup)
            mainWindow.NavigateToDatabaseImportFromStartup();

        // Fire-and-forget startup check (silent if not user-initiated)
        _ = CheckForUpdatesInteractiveAsync(mainWindow, userInitiated: false);

        // 4) Bootstrap backups once (folders + scheduled task)
        try
        {
            await BackupBootstrapper.TryBootstrapAsync();
        }
        catch
        {
            // Do NOT crash the app if bootstrap fails.
            // Optionally log.
        }
    }

    private static async Task<string> EnsureLocalServerSelectionAsync(DatabaseAppSettingsProvider settingsProvider)
    {
        var settings = settingsProvider.Settings;

        if (await CanConnectToServerAsync(settings.Local.Server))
            return settings.Local.Server;

        var sqlExpressUnavailable = string.Equals(settings.Local.Server, DefaultLocalSqlServer, StringComparison.OrdinalIgnoreCase);
        var availableServers = DiscoverLocalSqlServers();

        if (!sqlExpressUnavailable)
        {
            MessageBox.Show(
                $"Δεν ήταν δυνατή η σύνδεση με τον SQL Server '{settings.Local.Server}'.\nΕλέγξτε τη ρύθμιση και δοκιμάστε ξανά.",
                "Σύνδεση Βάσης",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return settings.Local.Server;
        }

        var onlyDefaultCandidate = availableServers.Count == 1 &&
                                   string.Equals(availableServers[0], DefaultLocalSqlServer, StringComparison.OrdinalIgnoreCase);

        if (availableServers.Count == 0 || onlyDefaultCandidate)
        {
            ShowWarningWithDownload(
                "Δεν βρέθηκε διαθέσιμο local SQL instance.\nΕγκαταστήστε SQL Server/SQLEXPRESS και επανεκκινήστε την εφαρμογή.",
                "Σύνδεση Βάσης",
                SqlExpressDownloadUrl,
                "Λήψη SQL Express");
            return settings.Local.Server;
        }

        var picker = Services.GetRequiredService<Windows.LocalSqlServerPickerWindow>();
        picker.Initialize(availableServers, settings.Local.Server);
        var result = picker.ShowDialog();
        if (result != true || string.IsNullOrWhiteSpace(picker.SelectedServer))
            return settings.Local.Server;

        if (await CanConnectToServerAsync(picker.SelectedServer))
            return picker.SelectedServer;

        MessageBox.Show(
            $"Αποτυχία σύνδεσης με τον SQL Server '{picker.SelectedServer}'.\nΗ εφαρμογή θα συνεχίσει χωρίς local βάση.",
            "Σύνδεση Βάσης",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return settings.Local.Server;
    }

    private static async Task<bool> CanConnectToServerAsync(string server)
    {
        try
        {
            var connectionString = DatabaseAppSettingsProvider.BuildTrustedConnectionString(server, "master");
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTailscaleInstalled()
    {
        if (File.Exists(@"C:\Program Files\Tailscale\tailscale.exe") ||
            File.Exists(@"C:\Program Files (x86)\Tailscale\tailscale.exe"))
            return true;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "tailscale",
                Arguments = "version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(2000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(bool HasFilothei, bool HasNeaIonia, bool HasAny)> CheckLocalDatabasesAvailabilityAsync(string server)
    {
        var hasFilothei = await LocalDatabaseExistsAsync(server, "FilotheiSchoolERP");
        var hasNeaIonia = await LocalDatabaseExistsAsync(server, "NeaIoniaSchoolERP");
        return (hasFilothei, hasNeaIonia, hasFilothei || hasNeaIonia);
    }

    private static async Task<bool> LocalDatabaseExistsAsync(string server, string databaseName)
    {
        try
        {
            var connectionString = DatabaseAppSettingsProvider.BuildTrustedConnectionString(server, "master");
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("SELECT COUNT(1) FROM sys.databases WHERE name = @db", connection);
            command.Parameters.AddWithValue("@db", databaseName);
            var count = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
            return count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<ConnectivityCheckResult> CheckRemoteConnectivityAsync(DatabaseAppSettings settings, string remoteDatabaseName)
    {
        try
        {
            var targetDb = string.IsNullOrWhiteSpace(remoteDatabaseName)
                ? settings.Remote.Databases.FirstOrDefault()?.Database ?? settings.Startup.RemoteDatabase
                : remoteDatabaseName;

            var remoteConnection = ConnectionStringHelpers.ReplaceDatabase(settings.Remote.ConnectionString, targetDb);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            return await RemoteConnectivityDiagnostics.CheckRemoteSqlAsync(remoteConnection, cts.Token);
        }
        catch (Exception ex)
        {
            return ConnectivityCheckResult.Fail("Tailscale disconnected", ex.Message);
        }
    }

    private static async Task<(bool Success, int UpdatedDatabases)> TryAutoMigrateLocalDatabasesAsync(AppState appState)
    {
        if (!appState.HasAnyLocalDatabase)
            return (true, 0);

        var localDatabases = appState.AvailableLocalDatabases
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (localDatabases.Count == 0)
            return (true, 0);

        var previousMode = appState.SelectedDatabaseMode;
        var previousLocalDb = appState.SelectedLocalDatabaseName;
        var updatedDatabases = 0;
        Windows.MigrationProgressWindow? progressWindow = null;

        try
        {
            appState.SelectedDatabaseMode = DatabaseMode.Local;

            var databasesToMigrate = new List<string>();
            foreach (var localDbName in localDatabases)
            {
                appState.SelectedLocalDatabaseName = localDbName;

                await using var db = Services.GetRequiredService<DbContextFactory>().Create();
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                    databasesToMigrate.Add(localDbName);
            }

            if (databasesToMigrate.Count == 0)
            {
                UpdaterLog.Write("Migration", "No pending migrations found in local databases.");
                return (true, 0);
            }

            progressWindow = new Windows.MigrationProgressWindow();
            progressWindow.SetStatus("Παρακαλώ περιμένετε όσο ολοκληρώνεται η ενημέρωση. Η εφαρμογή θα ανοίξει αυτόματα.");
            progressWindow.Show();

            foreach (var localDbName in databasesToMigrate)
            {
                appState.SelectedLocalDatabaseName = localDbName;
                progressWindow.SetStatus($"Ενημέρωση βάσης '{localDbName}'...");

                var sw = Stopwatch.StartNew();
                try
                {
                    await using var db = Services.GetRequiredService<DbContextFactory>().Create();
                    await db.Database.MigrateAsync();
                    sw.Stop();

                    updatedDatabases++;
                    UpdaterLog.Write("Migration", $"Database '{localDbName}' migrated successfully in {sw.ElapsedMilliseconds} ms.");
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    UpdaterLog.Write("Migration", $"Database '{localDbName}' migration failed after {sw.ElapsedMilliseconds} ms.", ex);
                    throw;
                }
            }

            return (true, updatedDatabases);
        }
        catch (Exception ex)
        {
            UpdaterLog.Write("Migration", "Automatic local migration flow failed.", ex);
            return (false, updatedDatabases);
        }
        finally
        {
            progressWindow?.Close();
            appState.SelectedLocalDatabaseName = previousLocalDb;
            appState.SelectedDatabaseMode = previousMode;
        }
    }

    private static List<string> DiscoverLocalSqlServers()
    {
        var servers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var sources = SqlDataSourceEnumerator.Instance.GetDataSources();
            foreach (DataRow row in sources.Rows)
            {
                var serverName = row["ServerName"]?.ToString();
                var instanceName = row["InstanceName"]?.ToString();

                if (string.IsNullOrWhiteSpace(serverName))
                    continue;

                var fullName = string.IsNullOrWhiteSpace(instanceName)
                    ? serverName.Trim()
                    : $"{serverName.Trim()}\\{instanceName.Trim()}";

                servers.Add(fullName);
            }
        }
        catch
        {
            // Ignore discovery errors and return whatever we have.
        }

        servers.Add(DefaultLocalSqlServer);
        return servers.OrderBy(x => x).ToList();
    }

    private static void ShowWarningWithDownload(string message, string caption, string downloadUrl, string buttonText)
    {
        var choice = MessageBox.Show(
            $"{message}\n\nΘέλετε να ανοίξει η σελίδα: {buttonText};",
            caption,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (choice != MessageBoxResult.Yes)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = downloadUrl,
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(
                "Δεν ήταν δυνατό να ανοίξει ο browser. Αντιγράψτε το link χειροκίνητα από το μήνυμα.",
                caption,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
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

            var updaterStarted = await DownloadAndRunUpdaterAsync(result, owner);

            if (!updaterStarted)
            {
                return;
            }

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

    private static async Task<bool> DownloadAndRunUpdaterAsync(UpdateCheckResult result, Window owner)
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

        if (!CanWriteToInstallDirectory(installDir, out var writeError))
        {
            UpdaterLog.Write("App", $"Insufficient permissions for install directory '{installDir}'. {writeError}");
            MessageBox.Show(owner,
                "Δεν υπάρχουν επαρκή δικαιώματα εγγραφής στον φάκελο εγκατάστασης για την ενημέρωση.\n\n" +
                "Δοκιμάστε να εκτελέσετε την εφαρμογή ως διαχειριστής ή εγκαταστήστε την εφαρμογή σε φάκελο με δικαιώματα εγγραφής για τον χρήστη.",
                "Ανεπαρκή δικαιώματα",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

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

        return true;
    }

    private static bool CanWriteToInstallDirectory(string installDir, out string? error)
    {
        var probeFilePath = Path.Combine(installDir, $".write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probeFilePath, "write-test");
            File.Delete(probeFilePath);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
        {
            error = ex.Message;
            try
            {
                if (File.Exists(probeFilePath))
                {
                    File.Delete(probeFilePath);
                }
            }
            catch
            {
                // ignore cleanup failures
            }

            return false;
        }
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
