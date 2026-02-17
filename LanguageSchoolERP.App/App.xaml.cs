using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using LanguageSchoolERP.Services;
using LanguageSchoolERP.App.ViewModels;
using LanguageSchoolERP.App.Views;

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

        services.AddSingleton<ProgramsViewModel>();
        services.AddSingleton<ProgramsView>();

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

        // Services layer
        services.AddSingleton<ReceiptNumberService>();
        services.AddSingleton<ExcelReceiptGenerator>();
        services.AddSingleton<ContractDocumentService>();
        services.AddSingleton<ContractBookmarkBuilder>();

        // Global settings/state
        services.AddSingleton<DatabaseAppSettingsProvider>();
        services.AddSingleton<AppState>();

        // DbContext factory (runtime)
        services.AddSingleton<DbContextFactory>();

        // Main window
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }
}
