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
    private readonly DbContextFactory _dbFactory;
    private readonly StudentContactsExcelExportService _exportService;
    private readonly List<StudentContactsGridRowVm> _allRows = [];
    private readonly Dictionary<Guid, StudentContactsGridRowVm> _selectedRowsById = [];

    public ObservableCollection<StudentContactsGridRowVm> Students { get; } = [];
    public ObservableCollection<string> AcademicYears { get; } = [];
    public ObservableCollection<SelectedStudentRowVm> SelectedStudents { get; } = [];

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedAcademicYear = string.Empty;
    [ObservableProperty] private string errorMessage = string.Empty;

    [ObservableProperty] private bool includeStudentEmail;
    [ObservableProperty] private bool includeStudentMobile;
    [ObservableProperty] private bool includeStudentLandline;

    [ObservableProperty] private bool includeFatherName;
    [ObservableProperty] private bool includeMotherName;
    [ObservableProperty] private bool includeFatherEmail;
    [ObservableProperty] private bool includeFatherMobile;
    [ObservableProperty] private bool includeFatherLandline;
    [ObservableProperty] private bool includeMotherEmail;
    [ObservableProperty] private bool includeMotherMobile;
    [ObservableProperty] private bool includeMotherLandline;
    [ObservableProperty] private bool includeStudentDateOfBirth;
    [ObservableProperty] private bool includeSchoolName;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IRelayCommand ExportCommand { get; }

    public StudentContactsExportViewModel(AppState state, DbContextFactory dbFactory, StudentContactsExcelExportService exportService)
    {
        _dbFactory = dbFactory;
        _exportService = exportService;
        selectedAcademicYear = state.SelectedAcademicYear;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SelectAllCommand = new RelayCommand(SelectAll);
        ClearSelectionCommand = new RelayCommand(ClearSelection);
        ExportCommand = new RelayCommand(ExportSelected);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedAcademicYearChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        _ = LoadStudentsForYearAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            await LoadAcademicYearsAsync();
            await LoadStudentsForYearAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Αποτυχία φόρτωσης μαθητών: {ex.Message}";
            Students.Clear();
        }
    }

    private async Task LoadAcademicYearsAsync()
    {
        await using var db = _dbFactory.Create();

        var years = await db.AcademicPeriods
            .AsNoTracking()
            .OrderByDescending(p => p.Name)
            .Select(p => p.Name)
            .ToListAsync();

        AcademicYears.Clear();
        foreach (var year in years)
            AcademicYears.Add(year);

        if (AcademicYears.Count == 0)
        {
            SelectedAcademicYear = string.Empty;
            return;
        }

        if (!AcademicYears.Contains(SelectedAcademicYear))
            SelectedAcademicYear = AcademicYears[0];
    }

    private async Task LoadStudentsForYearAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedAcademicYear))
        {
            _allRows.Clear();
            Students.Clear();
            return;
        }

        await using var db = _dbFactory.Create();

        var enrollmentsForYear = await db.Enrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Include(e => e.Program)
            .Include(e => e.AcademicPeriod)
            .Where(e => e.AcademicPeriod.Name == SelectedAcademicYear)
            .OrderByDescending(e => e.EnrollmentId)
            .ToListAsync();

        _allRows.Clear();

        foreach (var enrollment in enrollmentsForYear
                     .GroupBy(e => e.StudentId)
                     .Select(g => g.First())
                     .OrderBy(x => x.Student.FullName))
        {
            var student = enrollment.Student;
            var row = new StudentContactsGridRowVm
            {
                StudentId = student.StudentId,
                StudentName = student.FullName,
                ProgramName = enrollment.Program?.Name ?? string.Empty,
                LevelOrClass = enrollment.LevelOrClass ?? string.Empty,
                AcademicYear = SelectedAcademicYear,
                StudentDateOfBirth = student.DateOfBirth?.ToString("dd/MM/yyyy") ?? string.Empty,
                SchoolName = student.SchoolName,
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
                MotherLandline = student.MotherLandline,
                SelectionChanged = OnRowSelectionChanged
            };

            row.IsSelected = _selectedRowsById.ContainsKey(row.StudentId);
            _allRows.Add(row);
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
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
            Students.Add(student);
    }

    private void OnRowSelectionChanged(StudentContactsGridRowVm row, bool isSelected)
    {
        if (isSelected)
        {
            _selectedRowsById[row.StudentId] = row.CloneForExport();
        }
        else
        {
            _selectedRowsById.Remove(row.StudentId);
        }

        RefreshSelectedStudents();
    }

    private void RefreshSelectedStudents()
    {
        SelectedStudents.Clear();
        foreach (var row in _selectedRowsById.Values.OrderBy(x => x.StudentName))
        {
            SelectedStudents.Add(new SelectedStudentRowVm
            {
                StudentId = row.StudentId,
                StudentName = row.StudentName,
                AcademicYear = row.AcademicYear
            });
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
        var selected = _selectedRowsById.Values.OrderBy(x => x.StudentName).ToList();
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

        if (IncludeStudentDateOfBirth) headers.Add("Ημ/νία Γέννησης Μαθητή");
        if (IncludeStudentEmail) headers.Add("Email Μαθητή");
        if (IncludeStudentMobile) headers.Add("Κινητό Μαθητή");
        if (IncludeStudentLandline) headers.Add("Σταθερό Μαθητή");
        if (IncludeSchoolName) headers.Add("Σχολείο");

        if (IncludeFatherName) headers.Add("Ονοματεπώνυμο Πατέρα");
        if (IncludeMotherName) headers.Add("Ονοματεπώνυμο Μητέρας");

        if (IncludeFatherEmail) headers.Add("Email Πατέρα");
        if (IncludeFatherMobile) headers.Add("Κινητό Πατέρα");
        if (IncludeFatherLandline) headers.Add("Σταθερό Πατέρα");

        if (IncludeMotherEmail) headers.Add("Email Μητέρας");
        if (IncludeMotherMobile) headers.Add("Κινητό Μητέρας");
        if (IncludeMotherLandline) headers.Add("Σταθερό Μητέρας");

        return headers;
    }

    private List<string> BuildRowValues(StudentContactsGridRowVm s)
    {
        var values = new List<string> { s.StudentName, s.ProgramName, s.LevelOrClass };

        if (IncludeStudentDateOfBirth) values.Add(s.StudentDateOfBirth);
        if (IncludeStudentEmail) values.Add(s.StudentEmail);
        if (IncludeStudentMobile) values.Add(s.StudentMobile);
        if (IncludeStudentLandline) values.Add(s.StudentLandline);
        if (IncludeSchoolName) values.Add(s.SchoolName);

        if (IncludeFatherName) values.Add(s.FatherName);
        if (IncludeMotherName) values.Add(s.MotherName);

        if (IncludeFatherEmail) values.Add(s.FatherEmail);
        if (IncludeFatherMobile) values.Add(s.FatherMobile);
        if (IncludeFatherLandline) values.Add(s.FatherLandline);

        if (IncludeMotherEmail) values.Add(s.MotherEmail);
        if (IncludeMotherMobile) values.Add(s.MotherMobile);
        if (IncludeMotherLandline) values.Add(s.MotherLandline);

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
    [ObservableProperty] private string academicYear = string.Empty;

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

    [ObservableProperty] private string studentDateOfBirth = string.Empty;
    [ObservableProperty] private string schoolName = string.Empty;

    public Action<StudentContactsGridRowVm, bool>? SelectionChanged { get; init; }

    partial void OnIsSelectedChanged(bool value)
    {
        SelectionChanged?.Invoke(this, value);
    }

    public StudentContactsGridRowVm CloneForExport()
    {
        return new StudentContactsGridRowVm
        {
            StudentId = StudentId,
            StudentName = StudentName,
            ProgramName = ProgramName,
            LevelOrClass = LevelOrClass,
            AcademicYear = AcademicYear,
            StudentDateOfBirth = StudentDateOfBirth,
            SchoolName = SchoolName,
            StudentEmail = StudentEmail,
            StudentMobile = StudentMobile,
            StudentLandline = StudentLandline,
            FatherName = FatherName,
            FatherEmail = FatherEmail,
            FatherMobile = FatherMobile,
            FatherLandline = FatherLandline,
            MotherName = MotherName,
            MotherEmail = MotherEmail,
            MotherMobile = MotherMobile,
            MotherLandline = MotherLandline
        };
    }
}

public sealed class SelectedStudentRowVm
{
    public Guid StudentId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string AcademicYear { get; init; } = string.Empty;
}
