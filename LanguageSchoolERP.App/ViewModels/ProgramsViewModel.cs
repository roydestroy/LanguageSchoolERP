using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;

namespace LanguageSchoolERP.App.ViewModels;

public partial class ProgramsViewModel : ObservableObject
{
    public ObservableCollection<string> Programs { get; } = new();

    [ObservableProperty] private string newProgramName = "";

    public IRelayCommand AddProgramCommand { get; }

    public ProgramsViewModel()
    {
        AddProgramCommand = new RelayCommand(AddProgram);

        Programs.Add(ProgramType.LanguageSchool.ToDisplayName());
        Programs.Add(ProgramType.StudyLab.ToDisplayName());
        Programs.Add(ProgramType.EuroLab.ToDisplayName());
    }

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
}
