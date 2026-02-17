using System.Windows;
using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.App.Windows;

namespace LanguageSchoolERP.App.Views;

public partial class ProgramsView : UserControl
{
    private readonly ProgramsListViewModel _vm;

    public ProgramsView(ProgramsListViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += ProgramsView_Loaded;
    }

    private async void ProgramsView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _vm.LoadAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _vm.ErrorMessage = ex.Message;
        }
    }

    private async void AddProgramButton_Click(object sender, RoutedEventArgs e)
    {
        var editVm = new ProgramEditViewModel();
        var dialog = new ProgramEditWindow(editVm) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _vm.AddAsync(new StudyProgram
            {
                Name = editVm.Name,
                HasTransport = editVm.HasTransport,
                HasStudyLab = editVm.HasStudyLab,
                HasBooks = editVm.HasBooks
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _vm.ErrorMessage = ex.Message;
        }
    }

    private async void EditProgramButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProgram is null)
        {
            _vm.ErrorMessage = "Επιλέξτε πρόγραμμα για επεξεργασία.";
            return;
        }

        var editVm = new ProgramEditViewModel
        {
            Id = _vm.SelectedProgram.Id,
            Name = _vm.SelectedProgram.Name,
            HasTransport = _vm.SelectedProgram.HasTransport,
            HasStudyLab = _vm.SelectedProgram.HasStudyLab,
            HasBooks = _vm.SelectedProgram.HasBooks
        };

        var dialog = new ProgramEditWindow(editVm) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _vm.UpdateAsync(new StudyProgram
            {
                Id = editVm.Id,
                Name = editVm.Name,
                HasTransport = editVm.HasTransport,
                HasStudyLab = editVm.HasStudyLab,
                HasBooks = editVm.HasBooks
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _vm.ErrorMessage = ex.Message;
        }
    }

    private async void DeleteProgramButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProgram is null)
        {
            _vm.ErrorMessage = "Επιλέξτε πρόγραμμα για διαγραφή.";
            return;
        }

        var result = MessageBox.Show(
            $"Να διαγραφεί το πρόγραμμα '{_vm.SelectedProgram.Name}';",
            "Επιβεβαίωση διαγραφής",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _vm.DeleteSelectedAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _vm.ErrorMessage = ex.Message;
        }
    }
}
