using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using LanguageSchoolERP.App.ViewModels;

namespace LanguageSchoolERP.App.Windows;

public partial class StudentProfileWindow : Window
{
    private readonly StudentProfileViewModel _vm;
    private int _lastSelectedTabIndex;
    private bool _isRevertingTabSelection;

    public StudentProfileWindow(StudentProfileViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _vm.RequestClose += HandleRequestClose;
        Closed += OnWindowClosed;
        Closing += OnWindowClosing;
        DataContext = vm;
        _lastSelectedTabIndex = 0;
    }

    private void HandleRequestClose()
    {
        Close();
    }


    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _vm.RequestClose -= HandleRequestClose;

        var ownerWindow = Owner ?? Application.Current?.MainWindow;
        if (ownerWindow is null)
            return;

        ownerWindow.Activate();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_vm.ConfirmDiscardUnsavedProfileChanges())
            return;

        e.Cancel = true;
    }

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRevertingTabSelection)
            return;

        if (sender is not TabControl tabControl || !ReferenceEquals(e.Source, tabControl))
            return;

        if (_vm.ConfirmDiscardUnsavedProfileChanges())
        {
            _lastSelectedTabIndex = tabControl.SelectedIndex;
            return;
        }

        _isRevertingTabSelection = true;
        tabControl.SelectedIndex = _lastSelectedTabIndex;
        _isRevertingTabSelection = false;
    }

    public void Initialize(Guid studentId)
    {
        _vm.Initialize(studentId);
    }
}
