using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;

namespace LanguageSchoolERP.App.ViewModels;

public partial class ProgramsListViewModel : ObservableObject
{
    private readonly IProgramService _programService;
    private readonly AppState _state;
    private readonly List<StudyProgram> _allPrograms = new();

    public ObservableCollection<StudyProgram> Programs { get; } = new();

    [ObservableProperty] private StudyProgram? selectedProgram;
    [ObservableProperty] private string errorMessage = string.Empty;
    [ObservableProperty] private string searchText = string.Empty;

    public bool CanManagePrograms => !_state.IsReadOnlyMode;

    public ProgramsListViewModel(IProgramService programService, AppState state)
    {
        _programService = programService;
        _state = state;

        _state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.SelectedDatabaseMode))
                OnPropertyChanged(nameof(CanManagePrograms));

            if (ShouldReloadForContextChange(e.PropertyName))
                _ = ReloadForContextChangeAsync();
        };
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }


    private bool ShouldReloadForContextChange(string? propertyName)
    {
        return propertyName == nameof(AppState.SelectedDatabaseMode) ||
               propertyName == nameof(AppState.SelectedDatabaseName) ||
               propertyName == nameof(AppState.SelectedAcademicYear) ||
               propertyName == nameof(AppState.DataVersion);
    }

    private async Task ReloadForContextChangeAsync()
    {
        try
        {
            await LoadAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public async Task LoadAsync(CancellationToken ct)
    {
        ErrorMessage = string.Empty;
        var programs = await _programService.GetAllAsync(ct);

        _allPrograms.Clear();
        _allPrograms.AddRange(programs);

        ApplyFilter();
    }

    public async Task AddAsync(StudyProgram program, CancellationToken ct)
    {
        await _programService.CreateAsync(program, ct);
        await LoadAsync(ct);
    }

    public async Task UpdateAsync(StudyProgram program, CancellationToken ct)
    {
        await _programService.UpdateAsync(program, ct);
        await LoadAsync(ct);
    }

    public async Task DeleteSelectedAsync(CancellationToken ct)
    {
        if (SelectedProgram is null)
        {
            return;
        }

        await _programService.DeleteAsync(SelectedProgram.Id, ct);
        await LoadAsync(ct);
    }

    private void ApplyFilter()
    {
        var selectedId = SelectedProgram?.Id;

        IEnumerable<StudyProgram> filtered = _allPrograms;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            filtered = filtered.Where(p => p.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        }

        Programs.Clear();
        foreach (var program in filtered)
        {
            Programs.Add(program);
        }

        SelectedProgram = selectedId.HasValue
            ? Programs.FirstOrDefault(p => p.Id == selectedId.Value)
            : null;
    }
}
