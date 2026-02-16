using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using LanguageSchoolERP.Services;
using LanguageSchoolERP.App.ViewModels;
using LanguageSchoolERP.App.Views;

namespace LanguageSchoolERP.App;

public partial class App : Application
{
    private static readonly Uri DarkThemeUri = new("Themes/Colors.Dark.xaml", UriKind.Relative);

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

        // Global state
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

    public static void ApplyTheme(bool useDarkTheme)
    {
        var resources = Current.Resources.MergedDictionaries;
        var existing = resources.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.EndsWith("Colors.Dark.xaml", StringComparison.OrdinalIgnoreCase));

        if (useDarkTheme && existing == null)
        {
            resources.Add(new ResourceDictionary { Source = DarkThemeUri });
            return;
        }

        if (!useDarkTheme && existing != null)
        {
            resources.Remove(existing);
        }
    }
}
