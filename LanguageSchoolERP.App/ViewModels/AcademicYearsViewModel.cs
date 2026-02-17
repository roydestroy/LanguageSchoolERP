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
        using var db = _dbFactory.Create();
        DbSeeder.EnsureSeeded(db);

        var years = await db.AcademicPeriods
            .AsNoTracking()
            .OrderByDescending(x => x.Name)
            .ToListAsync();

        AcademicYears.Clear();
        foreach (var year in years)
        {
            AcademicYears.Add(year);
        }
    }

    private async Task AddAsync()
    {
        if (!CanWrite())
        {
            System.Windows.MessageBox.Show("Η απομακρυσμένη λειτουργία είναι μόνο για ανάγνωση.");
            return;
        }
        var name = (NewAcademicYearName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        using var db = _dbFactory.Create();
        var exists = await db.AcademicPeriods.AnyAsync(x => x.Name == name);
        if (exists)
        {
            return;
        }

        db.AcademicPeriods.Add(new AcademicPeriod { Name = name, IsCurrent = false });
        await db.SaveChangesAsync();

        _state.SelectedAcademicYear = name;
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
        {
            return;
        }

        using var db = _dbFactory.Create();
        var period = await db.AcademicPeriods.FirstOrDefaultAsync(x => x.AcademicPeriodId == SelectedAcademicYear.AcademicPeriodId);
        if (period is null)
        {
            return;
        }

        db.AcademicPeriods.Remove(period);
        await db.SaveChangesAsync();

        if (_state.SelectedAcademicYear == period.Name)
        {
            _state.SelectedAcademicYear = db.AcademicPeriods.OrderByDescending(x => x.Name).Select(x => x.Name).FirstOrDefault() ?? "";
        }

        await LoadAsync();
    }
}
