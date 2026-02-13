using System.Windows;
using LanguageSchoolERP.Services;
using Microsoft.Extensions.DependencyInjection;
using LanguageSchoolERP.App.Views;

namespace LanguageSchoolERP.App;

public partial class MainWindow : Window
{
    private readonly AppState _state;

    public MainWindow(AppState state)
    {
        InitializeComponent();
        _state = state;

        DbCombo.ItemsSource = new[]
        {
            "FilotheiSchoolERP",
            "NeaIoniaSchoolERP"
        };
        DbCombo.SelectedItem = _state.SelectedDatabaseName;

        YearCombo.ItemsSource = new[]
        {
            "2024-2025",
            "2025-2026"
        };
        YearCombo.SelectedItem = _state.SelectedAcademicYear;

        DbCombo.SelectionChanged += (_, __) =>
        {
            _state.SelectedDatabaseName = DbCombo.SelectedItem?.ToString() ?? _state.SelectedDatabaseName;
        };

        YearCombo.SelectionChanged += (_, __) =>
        {
            _state.SelectedAcademicYear = YearCombo.SelectedItem?.ToString() ?? _state.SelectedAcademicYear;
        };

        // Default screen
        NavigateToStudents();

        StudentsBtn.Click += (_, __) => NavigateToStudents();
    }

    private void NavigateToStudents()
    {
        var view = App.Services.GetRequiredService<StudentsView>();
        MainContent.Content = view;
    }
}
