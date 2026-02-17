using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;

namespace LanguageSchoolERP.App.ViewModels;

public partial class ProgramsViewModel : ObservableObject
{
    public ObservableCollection<string> Programs { get; } = new();

    [ObservableProperty] private string newProgramName = "";
    [ObservableProperty] private string? selectedProgram;

    public IRelayCommand AddProgramCommand { get; }
    public IRelayCommand DeleteProgramCommand { get; }

    public ProgramsViewModel()
    {
        AddProgramCommand = new RelayCommand(AddProgram);
        DeleteProgramCommand = new RelayCommand(DeleteProgram);

        Programs.Add(ProgramType.LanguageSchool.ToDisplayName());
        Programs.Add(ProgramType.StudyLab.ToDisplayName());
        Programs.Add(ProgramType.EuroLab.ToDisplayName());
    }

    public void AddProgramFromInput() => AddProgram();

    private void AddProgram()
    {
        var name = (NewProgramName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!Programs.Contains(name))
        {
            Programs.Add(name);
        }

        NewProgramName = "";
    }

    private void DeleteProgram()
    {
        if (string.IsNullOrWhiteSpace(SelectedProgram))
        {
            return;
        }

        Programs.Remove(SelectedProgram);
        SelectedProgram = null;
    }
}
