using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;

namespace LanguageSchoolERP.App.ViewModels;

public partial class ProgramsViewModel : ObservableObject
{
    public ObservableCollection<string> Programs { get; } = new();

    [ObservableProperty] private string? selectedProgram;

    public IRelayCommand DeleteProgramCommand { get; }

    public ProgramsViewModel()
    {
        DeleteProgramCommand = new RelayCommand(DeleteProgram);

        Programs.Add(ProgramType.LanguageSchool.ToDisplayName());
        Programs.Add(ProgramType.StudyLab.ToDisplayName());
        Programs.Add(ProgramType.EuroLab.ToDisplayName());
    }

    public bool AddProgram(string name)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (Programs.Contains(name))
        {
            return false;
        }

        Programs.Add(name);
        return true;
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
