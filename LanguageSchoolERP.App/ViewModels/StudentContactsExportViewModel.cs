using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Windows;

namespace LanguageSchoolERP.App.ViewModels;

public partial class StudentContactsExportViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly DbContextFactory _dbFactory;
    private readonly StudentContactsExcelExportService _exportService;
    private readonly List<StudentContactsGridRowVm> _allRows = [];

    public ObservableCollection<StudentContactsGridRowVm> Students { get; } = [];

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string errorMessage = string.Empty;

    [ObservableProperty] private bool includeStudentEmail = true;
    [ObservableProperty] private bool includeStudentMobile = true;
    [ObservableProperty] private bool includeStudentLandline = true;

    [ObservableProperty] private bool includeFatherName;
    [ObservableProperty] private bool includeMotherName;
    [ObservableProperty] private bool includeFatherEmail;
    [ObservableProperty] private bool includeFatherMobile;
    [ObservableProperty] private bool includeFatherLandline;
    [ObservableProperty] private bool includeMotherEmail;
    [ObservableProperty] private bool includeMotherMobile;
    [ObservableProperty] private bool includeMotherLandline;

    [ObservableProperty] private bool includeFirstName;
    [ObservableProperty] private bool includeLastName;
    [ObservableProperty] private bool includeDateOfBirth;
    [ObservableProperty] private bool includeNotes;
    [ObservableProperty] private bool includeDiscontinuedStatus;
    [ObservableProperty] private bool includeNonCollectableStatus;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IRelayCommand ExportCommand { get; }

    public StudentContactsExportViewModel(AppState state, DbContextFactory dbFactory, StudentContactsExcelExportService exportService)
    {
        _state = state;
        _dbFactory = dbFactory;
        _exportService = exportService;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SelectAllCommand = new RelayCommand(SelectAll);
        ClearSelectionCommand = new RelayCommand(ClearSelection);
        ExportCommand = new RelayCommand(ExportSelected);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public async Task LoadAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            await using var db = _dbFactory.Create();

            var students = await db.Students
                .AsNoTracking()
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Program)
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.AcademicPeriod)
                .ToListAsync();

            _allRows.Clear();
            foreach (var student in students)
            {
                var enrollment = student.Enrollments
                    .Where(e => e.AcademicPeriod?.Name == _state.SelectedAcademicYear)
                    .OrderByDescending(e => e.AcademicPeriod?.Name)
                    .ThenByDescending(e => e.EnrollmentId)
                    .FirstOrDefault()
                    ?? student.Enrollments
                        .OrderByDescending(e => e.AcademicPeriod?.Name)
                        .ThenByDescending(e => e.EnrollmentId)
                        .FirstOrDefault();

                _allRows.Add(new StudentContactsGridRowVm
                {
                    StudentId = student.StudentId,
                    StudentName = student.FullName,
                    ProgramName = enrollment?.Program?.Name ?? string.Empty,
                    LevelOrClass = enrollment?.LevelOrClass ?? string.Empty,
                    FirstName = student.FirstName,
                    LastName = student.LastName,
                    DateOfBirthText = student.DateOfBirth?.ToString("dd/MM/yyyy") ?? string.Empty,
                    Notes = student.Notes,
                    DiscontinuedStatus = student.Discontinued ? "Ναι" : "Όχι",
                    NonCollectableStatus = student.NonCollectable ? "Ναι" : "Όχι",
                    StudentEmail = student.Email,
                    StudentMobile = student.Mobile,
                    StudentLandline = student.Landline,
                    FatherName = student.FatherName,
                    FatherEmail = student.FatherEmail,
                    FatherMobile = student.FatherMobile,
                    FatherLandline = student.FatherLandline,
                    MotherName = student.MotherName,
                    MotherEmail = student.MotherEmail,
                    MotherMobile = student.MotherMobile,
                    MotherLandline = student.MotherLandline
                });
            }

            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Αποτυχία φόρτωσης μαθητών: {ex.Message}";
            Students.Clear();
        }
    }

    private void ApplyFilter()
    {
        var selectedIds = _allRows.Where(x => x.IsSelected).Select(x => x.StudentId).ToHashSet();
        IEnumerable<StudentContactsGridRowVm> filtered = _allRows;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            filtered = filtered.Where(s =>
                s.StudentName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || s.ProgramName.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || s.LevelOrClass.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        }

        Students.Clear();
        foreach (var student in filtered.OrderBy(s => s.StudentName))
        {
            student.IsSelected = selectedIds.Contains(student.StudentId);
            Students.Add(student);
        }
    }

    private void SelectAll()
    {
        foreach (var student in Students)
            student.IsSelected = true;
    }

    private void ClearSelection()
    {
        foreach (var student in Students)
            student.IsSelected = false;
    }

    private void ExportSelected()
    {
        var selected = _allRows.Where(x => x.IsSelected).OrderBy(x => x.StudentName).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Επιλέξτε τουλάχιστον έναν μαθητή.", "Εξαγωγή", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var headers = BuildHeaders();
        if (headers.Count == 0)
        {
            MessageBox.Show("Επιλέξτε τουλάχιστον ένα στοιχείο επικοινωνίας για εξαγωγή.", "Εξαγωγή", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            FileName = $"StudentContacts_{DateTime.Now:yyyyMMdd}",
            DefaultExt = ".xlsx",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        try
        {
            var rows = selected
                .Select(s => new StudentContactsExportRow { Values = BuildRowValues(s) })
                .ToList();

            _exportService.Export(saveDialog.FileName, rows, headers);

            MessageBox.Show("Η εξαγωγή ολοκληρώθηκε επιτυχώς.", "Εξαγωγή", MessageBoxButton.OK, MessageBoxImage.Information);
            Process.Start(new ProcessStartInfo { FileName = saveDialog.FileName, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Αποτυχία εξαγωγής: {ex.Message}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private List<string> BuildHeaders()
    {
        var headers = new List<string> { "Μαθητής", "Πρόγραμμα", "Τάξη/Επίπεδο" };

        if (IncludeStudentEmail) headers.Add("Email Μαθητή");
        if (IncludeStudentMobile) headers.Add("Κινητό Μαθητή");
        if (IncludeStudentLandline) headers.Add("Σταθερό Μαθητή");

        if (IncludeFatherName) headers.Add("Όνομα Πατέρα");
        if (IncludeMotherName) headers.Add("Όνομα Μητέρας");

        if (IncludeFatherEmail) headers.Add("Email Πατέρα");
        if (IncludeFatherMobile) headers.Add("Κινητό Πατέρα");
        if (IncludeFatherLandline) headers.Add("Σταθερό Πατέρα");

        if (IncludeMotherEmail) headers.Add("Email Μητέρας");
        if (IncludeMotherMobile) headers.Add("Κινητό Μητέρας");
        if (IncludeMotherLandline) headers.Add("Σταθερό Μητέρας");

        if (IncludeFirstName) headers.Add("Όνομα");
        if (IncludeLastName) headers.Add("Επώνυμο");
        if (IncludeDateOfBirth) headers.Add("Ημ/νία Γέννησης");
        if (IncludeNotes) headers.Add("Σημειώσεις");
        if (IncludeDiscontinuedStatus) headers.Add("Με διακοπή");
        if (IncludeNonCollectableStatus) headers.Add("Μη Εισπράξιμος");

        return headers;
    }

    private List<string> BuildRowValues(StudentContactsGridRowVm s)
    {
        var values = new List<string> { s.StudentName, s.ProgramName, s.LevelOrClass };

        if (IncludeStudentEmail) values.Add(s.StudentEmail);
        if (IncludeStudentMobile) values.Add(s.StudentMobile);
        if (IncludeStudentLandline) values.Add(s.StudentLandline);

        if (IncludeFatherName) values.Add(s.FatherName);
        if (IncludeMotherName) values.Add(s.MotherName);

        if (IncludeFatherEmail) values.Add(s.FatherEmail);
        if (IncludeFatherMobile) values.Add(s.FatherMobile);
        if (IncludeFatherLandline) values.Add(s.FatherLandline);

        if (IncludeMotherEmail) values.Add(s.MotherEmail);
        if (IncludeMotherMobile) values.Add(s.MotherMobile);
        if (IncludeMotherLandline) values.Add(s.MotherLandline);

        if (IncludeFirstName) values.Add(s.FirstName);
        if (IncludeLastName) values.Add(s.LastName);
        if (IncludeDateOfBirth) values.Add(s.DateOfBirthText);
        if (IncludeNotes) values.Add(s.Notes);
        if (IncludeDiscontinuedStatus) values.Add(s.DiscontinuedStatus);
        if (IncludeNonCollectableStatus) values.Add(s.NonCollectableStatus);

        return values;
    }
}

public partial class StudentContactsGridRowVm : ObservableObject
{
    public Guid StudentId { get; init; }

    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private string studentName = string.Empty;
    [ObservableProperty] private string programName = string.Empty;
    [ObservableProperty] private string levelOrClass = string.Empty;

    [ObservableProperty] private string studentEmail = string.Empty;
    [ObservableProperty] private string studentMobile = string.Empty;
    [ObservableProperty] private string studentLandline = string.Empty;

    [ObservableProperty] private string fatherName = string.Empty;
    [ObservableProperty] private string fatherEmail = string.Empty;
    [ObservableProperty] private string fatherMobile = string.Empty;
    [ObservableProperty] private string fatherLandline = string.Empty;

    [ObservableProperty] private string motherName = string.Empty;
    [ObservableProperty] private string motherEmail = string.Empty;
    [ObservableProperty] private string motherMobile = string.Empty;
    [ObservableProperty] private string motherLandline = string.Empty;

    [ObservableProperty] private string firstName = string.Empty;
    [ObservableProperty] private string lastName = string.Empty;
    [ObservableProperty] private string dateOfBirthText = string.Empty;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private string discontinuedStatus = string.Empty;
    [ObservableProperty] private string nonCollectableStatus = string.Empty;
}
