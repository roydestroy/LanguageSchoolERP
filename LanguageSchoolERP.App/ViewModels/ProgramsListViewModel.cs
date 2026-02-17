using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;

namespace LanguageSchoolERP.App.ViewModels;

public partial class ProgramsListViewModel : ObservableObject
{
    private readonly IProgramService _programService;

    public ObservableCollection<StudyProgram> Programs { get; } = new();

    [ObservableProperty] private StudyProgram? selectedProgram;
    [ObservableProperty] private string errorMessage = string.Empty;

    public ProgramsListViewModel(IProgramService programService)
    {
        _programService = programService;
    }

    public async Task LoadAsync(CancellationToken ct)
    {
        ErrorMessage = string.Empty;
        var programs = await _programService.GetAllAsync(ct);
        Programs.Clear();
        foreach (var program in programs)
        {
            Programs.Add(program);
        }
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
}
