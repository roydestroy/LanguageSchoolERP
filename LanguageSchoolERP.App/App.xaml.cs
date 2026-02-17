using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using LanguageSchoolERP.Services;
using LanguageSchoolERP.App.ViewModels;
using LanguageSchoolERP.App.Views;
using System.Threading.Tasks;

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

        // Services layer
        services.AddSingleton<ReceiptNumberService>();
        services.AddSingleton<ExcelReceiptGenerator>();
        services.AddSingleton<ContractDocumentService>();
        services.AddSingleton<ContractBookmarkBuilder>();
        services.AddTransient<IProgramService, ProgramService>();

        // Global settings/state
        services.AddSingleton<DatabaseAppSettingsProvider>();
        services.AddSingleton<AppState>();
        services.AddSingleton<IGitHubUpdateService, GitHubUpdateService>();

        // DbContext factory (runtime)
        services.AddSingleton<DbContextFactory>();

        // Main window
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        _ = CheckForUpdatesAsync(mainWindow);

        base.OnStartup(e);
    }

    private static async Task CheckForUpdatesAsync(Window owner)
    {
        var updateService = Services.GetRequiredService<IGitHubUpdateService>();
        var result = await updateService.CheckForUpdateAsync();

        if (!result.IsEnabled)
            return;

        if (!string.IsNullOrWhiteSpace(result.Error))
            return;

        if (!result.IsUpdateAvailable || string.IsNullOrWhiteSpace(result.ReleaseUrl))
            return;

        var releaseName = string.IsNullOrWhiteSpace(result.ReleaseName)
            ? result.ReleaseTag
            : result.ReleaseName;

        var message =
            $"Βρέθηκε νέα έκδοση ({releaseName}).\n" +
            $"Τρέχουσα: {result.CurrentVersion}\n" +
            $"Διαθέσιμη: {result.LatestVersion}\n\n" +
            "Θέλετε να ανοίξει η σελίδα release για ενημέρωση;";

        var choice = MessageBox.Show(owner, message, "Διαθέσιμη ενημέρωση", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (choice != MessageBoxResult.Yes)
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = result.ReleaseUrl,
            UseShellExecute = true
        });
    }
}
