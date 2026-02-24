using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;

namespace LanguageSchoolERP.App.ViewModels;

public partial class AcademicYearsViewModel : ObservableObject
{
    private readonly DbContextFactory _dbFactory;
    private readonly AppState _state;

    public ObservableCollection<AcademicPeriod> AcademicYears { get; } = new();

    [ObservableProperty] private string newAcademicYearName = "";
    [ObservableProperty] private AcademicPeriod? selectedAcademicYear;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand AddAcademicYearCommand { get; }
    public IAsyncRelayCommand DeleteAcademicYearCommand { get; }

    public AcademicYearsViewModel(DbContextFactory dbFactory, AppState state)
    {
        _dbFactory = dbFactory;
        _state = state;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);

        // ✅ Use textbox value (NewAcademicYearName)
        AddAcademicYearCommand = new AsyncRelayCommand(AddAsync, CanWrite);

        DeleteAcademicYearCommand = new AsyncRelayCommand(DeleteAsync, CanWrite);

        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.SelectedDatabaseMode))
            {
                AddAcademicYearCommand.NotifyCanExecuteChanged();
                DeleteAcademicYearCommand.NotifyCanExecuteChanged();
            }
        };

        _ = LoadAsync();
    }

    private bool CanWrite() => !_state.IsReadOnlyMode;

    private async Task LoadAsync()
    {
        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var years = await db.AcademicPeriods
                .AsNoTracking()
                .OrderByDescending(x => x.Name)
                .ToListAsync();

            AcademicYears.Clear();
            foreach (var year in years) AcademicYears.Add(year);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Σφάλμα φόρτωσης ακαδημαϊκών ετών:\n{ex.Message}",
                "Σφάλμα",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }



    public string? GetLatestAcademicYearName()
    {
        static int ExtractStartYear(string? value)
        {
            var firstPart = (value ?? string.Empty).Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return int.TryParse(firstPart, out var year) ? year : int.MinValue;
        }

        return AcademicYears
            .OrderByDescending(x => ExtractStartYear(x.Name))
            .ThenByDescending(x => x.Name)
            .Select(x => x.Name)
            .FirstOrDefault();
    }

    // Optional: keep this for external callers
    public Task AddAcademicYearAsync(string? yearName = null) => AddAsync(yearName);

    private async Task AddAsync()
    {
        try
        {
            await AddAsync(yearName: null);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Σφάλμα κατά την προσθήκη ακαδημαϊκού έτους:\n{ex.Message}",
                "Σφάλμα",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task AddAsync(string? yearName)
    {
        if (!CanWrite())
        {
            System.Windows.MessageBox.Show("Η απομακρυσμένη λειτουργία είναι μόνο για ανάγνωση.");
            return;
        }

        var name = (yearName ?? NewAcademicYearName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        using var db = _dbFactory.Create();
        DbSeeder.EnsureSeeded(db);

        // safer duplicate check (no ToUpper translation issues, relies on SQL collation)
        var exists = await db.AcademicPeriods.AsNoTracking().AnyAsync(x => x.Name == name);
        if (exists)
            return;

        db.AcademicPeriods.Add(new AcademicPeriod { Name = name, IsCurrent = false });
        await db.SaveChangesAsync();

        _state.SelectedAcademicYear = name;
        _state.NotifyDataChanged();
        NewAcademicYearName = "";
        await LoadAsync();
    }


    private async Task DeleteAsync()
    {
        if (!CanWrite())
        {
            System.Windows.MessageBox.Show("Η απομακρυσμένη λειτουργία είναι μόνο για ανάγνωση.");
            return;
        }

        if (SelectedAcademicYear is null)
            return;

        var confirmResult = System.Windows.MessageBox.Show(
            $"Είστε σίγουροι ότι θέλετε να διαγράψετε το ακαδημαϊκό έτος \"{SelectedAcademicYear.Name}\";",
            "Επιβεβαίωση διαγραφής",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirmResult != System.Windows.MessageBoxResult.Yes)
            return;

        using var db = _dbFactory.Create();
        DbSeeder.EnsureSeeded(db);

        var period = await db.AcademicPeriods
            .FirstOrDefaultAsync(x => x.AcademicPeriodId == SelectedAcademicYear.AcademicPeriodId);

        if (period is null)
            return;

        db.AcademicPeriods.Remove(period);
        await db.SaveChangesAsync();
        _state.NotifyDataChanged();

        if (_state.SelectedAcademicYear == period.Name)
        {
            _state.SelectedAcademicYear = await db.AcademicPeriods
                .AsNoTracking()
                .OrderByDescending(x => x.Name)
                .Select(x => x.Name)
                .FirstOrDefaultAsync() ?? "";
        }

        await LoadAsync();
    }
}
