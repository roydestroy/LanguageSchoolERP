using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;

namespace LanguageSchoolERP.App.ViewModels;

public sealed class ProgramGridRowVm
{
    public ProgramGridRowVm(StudyProgram program)
    {
        Program = program;
    }

    public StudyProgram Program { get; }

    public int Id => Program.Id;
    public string Name => Program.Name;
    public bool HasTransport => Program.HasTransport;
    public bool HasStudyLab => Program.HasStudyLab;
    public bool HasBooks => Program.HasBooks;

    public bool IsLegacyCompatible => ProgramTypeResolver.TryResolveLegacyType(Program, out _, out _);
    public string LegacyCompatibilityText => IsLegacyCompatible ? "Yes" : "No";

    public string? LegacyCompatibilityMessage
    {
        get
        {
            ProgramTypeResolver.TryResolveLegacyType(Program, out _, out var errorMessage);
            return errorMessage;
        }
    }
}

public partial class ProgramsListViewModel : ObservableObject
{
    private readonly IProgramService _programService;

    public ObservableCollection<ProgramGridRowVm> Programs { get; } = new();

    [ObservableProperty] private ProgramGridRowVm? selectedProgram;
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
            Programs.Add(new ProgramGridRowVm(program));
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
