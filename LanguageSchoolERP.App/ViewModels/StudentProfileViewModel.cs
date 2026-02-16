using CommunityToolkit.Mvvm.ComponentModel;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using LanguageSchoolERP.App.Windows;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.IO;

namespace LanguageSchoolERP.App.ViewModels;

public partial class StudentProfileViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly DbContextFactory _dbFactory;
    private readonly ExcelReceiptGenerator _excelReceiptGenerator;
    private readonly ContractDocumentService _contractDocumentService;

    private Guid _studentId;
    private bool _isLoading;

    private string _originalStudentName = "";
    private string _originalStudentSurname = "";
    private DateTime? _originalDateOfBirth;
    private string _originalPhone = "";
    private string _originalEmail = "";
    private string _originalFatherName = "";
    private string _originalFatherSurname = "";
    private string _originalFatherPhone = "";
    private string _originalFatherEmail = "";
    private string _originalMotherName = "";
    private string _originalMotherSurname = "";
    private string _originalMotherPhone = "";
    private string _originalMotherEmail = "";
    private string _originalNotes = "";

    public ObservableCollection<string> AvailableAcademicYears { get; } = new();
    public ObservableCollection<PaymentRowVm> Payments { get; } = new();
    public ObservableCollection<ReceiptRowVm> Receipts { get; } = new();
    public ObservableCollection<ProgramEnrollmentRowVm> Programs { get; } = new();
    public ObservableCollection<ContractRowVm> Contracts { get; } = new();
    [ObservableProperty] private ReceiptRowVm? selectedReceipt;
    [ObservableProperty] private ContractRowVm? selectedContract;
    [ObservableProperty] private string localAcademicYear = "";
    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private string contactLine = "";
    [ObservableProperty] private string activeStatusText = "Inactive";
    [ObservableProperty] private string notes = "";
    [ObservableProperty] private bool isEditing;

    [ObservableProperty] private string editableStudentName = "";
    [ObservableProperty] private string editableStudentSurname = "";
    [ObservableProperty] private DateTime? editableDateOfBirth;
    [ObservableProperty] private string editablePhone = "";
    [ObservableProperty] private string editableEmail = "";
    [ObservableProperty] private string editableFatherName = "";
    [ObservableProperty] private string editableFatherSurname = "";
    [ObservableProperty] private string editableFatherPhone = "";
    [ObservableProperty] private string editableFatherEmail = "";
    [ObservableProperty] private string editableMotherName = "";
    [ObservableProperty] private string editableMotherSurname = "";
    [ObservableProperty] private string editableMotherPhone = "";
    [ObservableProperty] private string editableMotherEmail = "";
    [ObservableProperty] private string editableNotes = "";

    [ObservableProperty] private string dobLine = "";
    [ObservableProperty] private string phoneLine = "";
    [ObservableProperty] private string emailLine = "";
    [ObservableProperty] private string fatherLine = "";
    [ObservableProperty] private string motherLine = "";

    [ObservableProperty] private string enrollmentSummaryLine = "";
    [ObservableProperty] private string agreementText = "0.00 €";
    [ObservableProperty] private string paidText = "0.00 €";
    [ObservableProperty] private string balanceText = "0.00 €";
    [ObservableProperty] private string progressText = "0%";
    [ObservableProperty] private double progressPercent = 0;
    [ObservableProperty] private string pendingContractsText = "";
    public IRelayCommand AddPaymentCommand { get; }
    public IRelayCommand CreateContractCommand { get; }
    public IRelayCommand PrintReceiptCommand { get; }
    public IRelayCommand EditContractCommand { get; }
    public IRelayCommand ExportContractPdfCommand { get; }
    public IRelayCommand DeleteContractCommand { get; }
    public IRelayCommand EditProfileCommand { get; }
    public IAsyncRelayCommand SaveProfileCommand { get; }
    public IRelayCommand CancelEditCommand { get; }
    public IAsyncRelayCommand DeleteStudentCommand { get; }
    public IRelayCommand AddProgramCommand { get; }
    public IRelayCommand<ProgramEnrollmentRowVm> EditProgramCommand { get; }
    public IAsyncRelayCommand<ProgramEnrollmentRowVm> RemoveProgramCommand { get; }

    public event Action? RequestClose;
    public StudentProfileViewModel(
        AppState state,
        DbContextFactory dbFactory,
        ExcelReceiptGenerator excelReceiptGenerator,
        ContractDocumentService contractDocumentService)
    {
        _state = state;
        _dbFactory = dbFactory;
        _excelReceiptGenerator = excelReceiptGenerator;
        _contractDocumentService = contractDocumentService;

        AddPaymentCommand = new RelayCommand(OpenAddPaymentDialog);
        CreateContractCommand = new RelayCommand(OpenCreateContractDialog);
        PrintReceiptCommand = new RelayCommand(() => _ = PrintSelectedReceiptAsync());
        EditContractCommand = new RelayCommand(EditSelectedContract, () => SelectedContract is not null);
        ExportContractPdfCommand = new RelayCommand(() => _ = ExportSelectedContractPdfAsync(), () => SelectedContract is not null);
        DeleteContractCommand = new RelayCommand(() => _ = DeleteSelectedContractAsync(), () => SelectedContract is not null);
        EditProfileCommand = new RelayCommand(StartEdit);
        SaveProfileCommand = new AsyncRelayCommand(SaveProfileAsync);
        CancelEditCommand = new RelayCommand(CancelEdit);
        DeleteStudentCommand = new AsyncRelayCommand(DeleteStudentAsync);
        AddProgramCommand = new RelayCommand(OpenAddProgramDialog);
        EditProgramCommand = new RelayCommand<ProgramEnrollmentRowVm>(OpenEditProgramDialog);
        RemoveProgramCommand = new AsyncRelayCommand<ProgramEnrollmentRowVm>(RemoveProgramAsync);

    }


    partial void OnSelectedContractChanged(ContractRowVm? value)
    {
        EditContractCommand.NotifyCanExecuteChanged();
        ExportContractPdfCommand.NotifyCanExecuteChanged();
        DeleteContractCommand.NotifyCanExecuteChanged();
    }

    private void StartEdit()
    {
        IsEditing = true;
    }

    private void CancelEdit()
    {
        EditableStudentName = _originalStudentName;
        EditableStudentSurname = _originalStudentSurname;
        EditableDateOfBirth = _originalDateOfBirth;
        EditablePhone = _originalPhone;
        EditableEmail = _originalEmail;
        EditableFatherName = _originalFatherName;
        EditableFatherSurname = _originalFatherSurname;
        EditableFatherPhone = _originalFatherPhone;
        EditableFatherEmail = _originalFatherEmail;
        EditableMotherName = _originalMotherName;
        EditableMotherSurname = _originalMotherSurname;
        EditableMotherPhone = _originalMotherPhone;
        EditableMotherEmail = _originalMotherEmail;
        EditableNotes = _originalNotes;
        IsEditing = false;
    }

    private async Task SaveProfileAsync()
    {
        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var student = await db.Students
                .FirstOrDefaultAsync(s => s.StudentId == _studentId);

            if (student is null)
            {
                System.Windows.MessageBox.Show("Student not found.");
                return;
            }

            student.FullName = JoinName(EditableStudentName, EditableStudentSurname);
            student.DateOfBirth = EditableDateOfBirth;
            student.Phone = EditablePhone.Trim();
            student.Email = EditableEmail.Trim();
            student.FatherName = JoinName(EditableFatherName, EditableFatherSurname);
            student.FatherContact = JoinPhoneEmail(EditableFatherPhone, EditableFatherEmail);
            student.MotherName = JoinName(EditableMotherName, EditableMotherSurname);
            student.MotherContact = JoinPhoneEmail(EditableMotherPhone, EditableMotherEmail);
            student.Notes = EditableNotes.Trim();

            await db.SaveChangesAsync();
            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "Save profile failed");
        }
    }

    private async Task DeleteStudentAsync()
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to delete this student? This action cannot be undone.",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var student = await db.Students
                .FirstOrDefaultAsync(s => s.StudentId == _studentId);

            if (student is null)
            {
                System.Windows.MessageBox.Show("Student not found.");
                return;
            }

            db.Students.Remove(student);
            await db.SaveChangesAsync();

            System.Windows.MessageBox.Show("Student deleted successfully.");
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "Delete student failed");
        }
    }

    private void OpenAddProgramDialog()
    {
        var win = App.Services.GetRequiredService<AddProgramEnrollmentWindow>();
        win.Owner = System.Windows.Application.Current.MainWindow;
        win.Initialize(new AddProgramEnrollmentInit(_studentId, LocalAcademicYear));

        var result = win.ShowDialog();
        if (result == true)
            _ = LoadAsync();
    }


    private void OpenEditProgramDialog(ProgramEnrollmentRowVm? row)
    {
        if (row is null) return;

        var win = App.Services.GetRequiredService<AddProgramEnrollmentWindow>();
        win.Owner = System.Windows.Application.Current.MainWindow;
        win.Initialize(new AddProgramEnrollmentInit(_studentId, LocalAcademicYear, row.EnrollmentId));

        var result = win.ShowDialog();
        if (result == true)
            _ = LoadAsync();
    }

    private async Task RemoveProgramAsync(ProgramEnrollmentRowVm? row)
    {
        if (row is null) return;

        var result = System.Windows.MessageBox.Show(
            "Remove this program enrollment from the student?",
            "Confirm Remove Program",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var enrollment = await db.Enrollments
                .FirstOrDefaultAsync(e => e.EnrollmentId == row.EnrollmentId && e.StudentId == _studentId);

            if (enrollment is null)
            {
                System.Windows.MessageBox.Show("Enrollment not found.");
                return;
            }

            db.Enrollments.Remove(enrollment);
            await db.SaveChangesAsync();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "Remove program failed");
        }
    }

    public void Initialize(Guid studentId)
    {
        _studentId = studentId;

        // Local selector defaults to global selection on open
        LocalAcademicYear = _state.SelectedAcademicYear;

        _ = LoadAvailableYearsAsync();
        _ = LoadAsync();
    }
    private void OpenAddPaymentDialog()
    {
        var win = App.Services.GetRequiredService<AddPaymentWindow>();
        win.Owner = System.Windows.Application.Current.MainWindow;

        // Use local year currently selected inside profile
        win.Initialize(new AddPaymentInit(_studentId, LocalAcademicYear));

        var result = win.ShowDialog();
        if (result == true)
        {
            _ = LoadAsync(); // refresh payments + balance
        }
    }
    private void OpenCreateContractDialog()
    {
        var win = App.Services.GetRequiredService<AddContractWindow>();
        win.Owner = System.Windows.Application.Current.MainWindow;

        win.Initialize(new AddContractInit(_studentId, LocalAcademicYear, ResolveBranchKey()));

        var result = win.ShowDialog();
        if (result == true)
        {
            _ = LoadAsync();
        }
    }

    private string ResolveBranchKey()
    {
        return _state.SelectedDatabaseName switch
        {
            "FilotheiSchoolERP" => "FILOTHEI",
            "NeaIoniaSchoolERP" => "NEA_IONIA",
            _ => "FILOTHEI"
        };
    }


    private void EditSelectedContract()
    {
        if (SelectedContract is null)
        {
            System.Windows.MessageBox.Show("Please select a contract first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedContract.DocxPath) || !File.Exists(SelectedContract.DocxPath))
        {
            System.Windows.MessageBox.Show("The contract DOCX file was not found.");
            return;
        }

        try
        {
            _contractDocumentService.OpenDocumentInWord(SelectedContract.DocxPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "Open contract failed");
        }
    }

    private async Task ExportSelectedContractPdfAsync()
    {
        if (SelectedContract is null)
        {
            System.Windows.MessageBox.Show("Please select a contract first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedContract.DocxPath) || !File.Exists(SelectedContract.DocxPath))
        {
            System.Windows.MessageBox.Show("The contract DOCX file was not found.");
            return;
        }

        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var contract = await db.Contracts.FirstOrDefaultAsync(c => c.ContractId == SelectedContract.ContractId);
            if (contract is null)
            {
                System.Windows.MessageBox.Show("Contract not found in database.");
                return;
            }

            var pdfPath = ContractPathService.GetContractPdfPathFromDocxPath(SelectedContract.DocxPath);
            _contractDocumentService.ExportPdfWithPageDuplication(SelectedContract.DocxPath, pdfPath);

            contract.PdfPath = pdfPath;
            await db.SaveChangesAsync();

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = pdfPath,
                UseShellExecute = true
            });

            await LoadAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "Export contract PDF failed");
        }
    }


    private async Task DeleteSelectedContractAsync()
    {
        if (SelectedContract is null)
        {
            System.Windows.MessageBox.Show("Please select a contract first.");
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            "Delete selected contract? This will remove it from the list and delete generated files if they exist.",
            "Delete Contract",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var contract = await db.Contracts.FirstOrDefaultAsync(c => c.ContractId == SelectedContract.ContractId);
            if (contract is null)
            {
                System.Windows.MessageBox.Show("Contract not found in database.");
                return;
            }

            TryDeleteFile(contract.DocxPath);
            TryDeleteFile(contract.PdfPath);

            db.Contracts.Remove(contract);
            await db.SaveChangesAsync();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "Delete contract failed");
        }
    }

    private async Task PrintSelectedReceiptAsync()
    {
        if (SelectedReceipt is null)
        {
            System.Windows.MessageBox.Show("Please select a receipt first.");
            return;
        }

        if (SelectedReceipt.IsDownpayment && !SelectedReceipt.HasPdf)
        {
            try
            {
                using var db = _dbFactory.Create();
                DbSeeder.EnsureSeeded(db);

                var enrollment = await db.Enrollments
                    .AsNoTracking()
                    .Include(e => e.Student)
                    .Include(e => e.AcademicPeriod)
                    .FirstOrDefaultAsync(e => e.EnrollmentId == SelectedReceipt.EnrollmentId);

                if (enrollment is null)
                {
                    System.Windows.MessageBox.Show("Enrollment not found for downpayment receipt.");
                    return;
                }

                var student = enrollment.Student;
                var academicYear = enrollment.AcademicPeriod.Name;

                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var receiptsRoot = Path.Combine(baseDir, "Receipts");

                var studentFolder = ReceiptPathService.GetStudentFolder(
                    baseDir: receiptsRoot,
                    dbName: _state.SelectedDatabaseName,
                    academicYear: academicYear,
                    studentFullName: student.FullName
                );

                var fileName = $"downpayment-{enrollment.EnrollmentId:N}.pdf";
                var pdfPath = Path.Combine(studentFolder, fileName);

                var templatePath = ReceiptTemplateResolver.GetTemplatePath(_state.SelectedDatabaseName);
                var issueDate = DateTime.Today;

                var data = new ReceiptPrintData(
                    ReceiptNumber: 0,
                    IssueDate: issueDate,
                    StudentName: student.FullName,
                    StudentPhone: student.Phone ?? "",
                    StudentEmail: student.Email ?? "",
                    Amount: enrollment.DownPayment,
                    PaymentMethod: "Enrollment Downpayment",
                    ProgramLabel: enrollment.ProgramType.ToDisplayName(),
                    AcademicYear: academicYear,
                    Notes: "Enrollment downpayment"
                );

                _excelReceiptGenerator.GenerateReceiptPdf(templatePath, pdfPath, data);

                SelectedReceipt.PdfPath = pdfPath;
                SelectedReceipt.HasPdf = true;
                SelectedReceipt.NumberText = "DP";
                SelectedReceipt.DateText = issueDate.ToString("dd/MM/yyyy");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.ToString(), "Downpayment receipt generation failed");
                return;
            }
        }

        if (!SelectedReceipt.HasPdf || string.IsNullOrWhiteSpace(SelectedReceipt.PdfPath))
        {
            System.Windows.MessageBox.Show("This receipt has no PDF yet. Generate PDF first (next step).");
            return;
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = SelectedReceipt.PdfPath,
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
    }


    partial void OnLocalAcademicYearChanged(string value)
    {
        _ = LoadAsync();
    }

    private async Task LoadAvailableYearsAsync()
    {
        using var db = _dbFactory.Create();
        DbSeeder.EnsureSeeded(db);

        var years = await db.AcademicPeriods
            .AsNoTracking()
            .OrderByDescending(p => p.Name)
            .Select(p => p.Name)
            .ToListAsync();

        AvailableAcademicYears.Clear();
        foreach (var y in years) AvailableAcademicYears.Add(y);

        if (!AvailableAcademicYears.Contains(LocalAcademicYear) && AvailableAcademicYears.Count > 0)
            LocalAcademicYear = AvailableAcademicYears[0];
    }

    private async Task LoadAsync()
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            Payments.Clear();
            Receipts.Clear();
            Programs.Clear();
            Contracts.Clear();
            SelectedContract = null;
            PendingContractsText = "";

            var period = await db.AcademicPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name == LocalAcademicYear);

            if (period is null)
            {
                PendingContractsText = "No pending contracts.";
                return;
            }

            var student = await db.Students
                .AsNoTracking()
                .Include(s => s.Enrollments.Where(e => e.AcademicPeriodId == period.AcademicPeriodId))
                    .ThenInclude(e => e.Payments)
                        .ThenInclude(p => p.Receipts)
                .Include(s => s.Enrollments.Where(e => e.AcademicPeriodId == period.AcademicPeriodId))
                    .ThenInclude(e => e.Payments)
                .FirstOrDefaultAsync(s => s.StudentId == _studentId);


            if (student is null) return;

            FullName = student.FullName;
            ContactLine = $"{student.Phone}  |  {student.Email}".Trim(' ', '|');
            Notes = student.Notes ?? "";

            var (studentName, studentSurname) = SplitName(student.FullName);
            var (fatherName, fatherSurname) = SplitName(student.FatherName);
            var (fatherPhone, fatherEmail) = SplitPhoneEmail(student.FatherContact);
            var (motherName, motherSurname) = SplitName(student.MotherName);
            var (motherPhone, motherEmail) = SplitPhoneEmail(student.MotherContact);

            _originalStudentName = studentName;
            _originalStudentSurname = studentSurname;
            _originalDateOfBirth = student.DateOfBirth;
            _originalPhone = student.Phone ?? "";
            _originalEmail = student.Email ?? "";
            _originalFatherName = fatherName;
            _originalFatherSurname = fatherSurname;
            _originalFatherPhone = fatherPhone;
            _originalFatherEmail = fatherEmail;
            _originalMotherName = motherName;
            _originalMotherSurname = motherSurname;
            _originalMotherPhone = motherPhone;
            _originalMotherEmail = motherEmail;
            _originalNotes = student.Notes ?? "";

            EditableStudentName = _originalStudentName;
            EditableStudentSurname = _originalStudentSurname;
            EditableDateOfBirth = _originalDateOfBirth;
            EditablePhone = _originalPhone;
            EditableEmail = _originalEmail;
            EditableFatherName = _originalFatherName;
            EditableFatherSurname = _originalFatherSurname;
            EditableFatherPhone = _originalFatherPhone;
            EditableFatherEmail = _originalFatherEmail;
            EditableMotherName = _originalMotherName;
            EditableMotherSurname = _originalMotherSurname;
            EditableMotherPhone = _originalMotherPhone;
            EditableMotherEmail = _originalMotherEmail;
            EditableNotes = _originalNotes;

            DobLine = student.DateOfBirth.HasValue ? $"DOB: {student.DateOfBirth:dd/MM/yyyy}" : "DOB: —";
            PhoneLine = string.IsNullOrWhiteSpace(student.Phone) ? "Phone: —" : $"Phone: {student.Phone}";
            EmailLine = string.IsNullOrWhiteSpace(student.Email) ? "Email: —" : $"Email: {student.Email}";
            FatherLine = $"Father: {student.FatherName}".Trim();
            MotherLine = $"Mother: {student.MotherName}".Trim();

            var hasAnyEnrollment = await db.Enrollments
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == _studentId);
            ActiveStatusText = hasAnyEnrollment ? "Active" : "Inactive";

            var enrollments = student.Enrollments.ToList();
            static string ProgramLabel(ProgramType p) => p.ToDisplayName();

            foreach (var e in enrollments.OrderBy(e => e.ProgramType).ThenBy(e => e.LevelOrClass))
            {
                Programs.Add(new ProgramEnrollmentRowVm
                {
                    EnrollmentId = e.EnrollmentId,
                    ProgramText = ProgramLabel(e.ProgramType),
                    LevelOrClassText = string.IsNullOrWhiteSpace(e.LevelOrClass) ? "—" : e.LevelOrClass,
                    AgreementTotalText = $"{e.AgreementTotal:0.00} €",
                    BooksText = $"{e.BooksAmount:0.00} €",
                    DownPaymentText = $"{e.DownPayment:0.00} €",
                    InstallmentsText = e.InstallmentCount > 0 && e.InstallmentStartMonth != null
                        ? BuildInstallmentPlanText(e)
                        : "—",
                    InstallmentAmountText = e.InstallmentCount > 0
                        ? BuildInstallmentAmountText(e)
                        : "—",
                    StatusText = string.IsNullOrWhiteSpace(e.Status) ? "Active" : e.Status,
                    CommentsText = string.IsNullOrWhiteSpace(e.Comments) ? "—" : e.Comments
                });
            }

            // Summary across enrollments (grouped by caret in list; here we aggregate)
            decimal agreementSum = enrollments.Sum(e => e.AgreementTotal);
            decimal downSum = enrollments.Sum(e => e.DownPayment);
            decimal paidSum = enrollments.Sum(e => e.Payments.Sum(p => p.Amount));
            decimal paidTotal = downSum + paidSum;
            decimal balance = agreementSum - paidTotal;
            if (balance < 0) balance = 0;

            // Base summary (program types)
            var programList = enrollments
                .Select(e => ProgramLabel(e.ProgramType))
                .Distinct()
                .ToList();

            var baseSummary = enrollments.Count == 0
                ? "No enrollments for this year."
                : $"{enrollments.Count} enrollment(s): {string.Join(", ", programList)}";

            // Installment plan summary
            var planParts = enrollments
                .Where(e => e.InstallmentCount > 0 && e.InstallmentStartMonth != null)
                .Select(e => $"{ProgramLabel(e.ProgramType)}: {BuildInstallmentPlanText(e)}")
                .ToList();

            var planText = planParts.Any()
                ? $" | Plan: {string.Join(" · ", planParts)}"
                : "";

            // Final line
            EnrollmentSummaryLine = baseSummary + planText;


            AgreementText = $"{agreementSum:0.00} €";
            PaidText = $"{paidTotal:0.00} €";
            BalanceText = $"{balance:0.00} €";

            var progress = agreementSum <= 0 ? 0 : (double)(paidTotal / agreementSum * 100m);
            if (progress > 100) progress = 100;
            if (progress < 0) progress = 0;

            ProgressPercent = progress;
            ProgressText = $"{progress:0}%";

            var enrollmentIds = enrollments.Select(e => e.EnrollmentId).ToList();
            var contractCreatedByEnrollment = await db.Contracts
                .AsNoTracking()
                .Where(c => enrollmentIds.Contains(c.EnrollmentId))
                .GroupBy(c => c.EnrollmentId)
                .Select(g => new { EnrollmentId = g.Key, CreatedAt = g.Min(c => c.CreatedAt) })
                .ToDictionaryAsync(x => x.EnrollmentId, x => x.CreatedAt);

            // Payments table (all payments in this year across enrollments)
            var paymentRows = enrollments
                .SelectMany(e => e.Payments.Select(p => new { Payment = p }))
                .OrderByDescending(x => x.Payment.PaymentDate)
                .ToList();

            foreach (var enrollment in enrollments.Where(e => e.DownPayment > 0))
            {
                var downpaymentDate = ResolveDownpaymentDateText(enrollment, contractCreatedByEnrollment);
                Payments.Add(new PaymentRowVm
                {
                    TypeText = "Downpayment",
                    DateText = downpaymentDate,
                    AmountText = $"{enrollment.DownPayment:0.00} €",
                    Method = "Enrollment",
                    ReasonText = "ΠΡΟΚΑΤΑΒΟΛΗ",
                    Notes = "Enrollment downpayment"
                });
            }

            foreach (var row in paymentRows)
            {
                Payments.Add(new PaymentRowVm
                {
                    TypeText = "Payment",
                    DateText = row.Payment.PaymentDate.ToString("dd/MM/yyyy"),
                    AmountText = $"{row.Payment.Amount:0.00} €",
                    Method = row.Payment.Method.ToString(),
                    ReasonText = ParseReason(row.Payment.Notes),
                    Notes = ParseAdditionalNotes(row.Payment.Notes)
                });
            }
            var receiptRows = enrollments
                .SelectMany(e => e.Payments.SelectMany(p => p.Receipts.Select(r => new { e, p, r })))
                .GroupBy(x => x.r.ReceiptId)
                .Select(g => g.First())
                .OrderByDescending(x => x.r.IssueDate)
                .ToList();

            foreach (var x in receiptRows)
            {
                Receipts.Add(new ReceiptRowVm
                {
                    NumberText = x.r.ReceiptNumber.ToString(),
                    DateText = x.r.IssueDate.ToString("dd/MM/yyyy"),
                    AmountText = $"{x.p.Amount:0.00} €",
                    MethodText = x.p.Method.ToString(),
                    ReasonText = ParseReason(x.p.Notes),
                    ProgramText = ProgramLabel(x.e.ProgramType),
                    HasPdf = !string.IsNullOrWhiteSpace(x.r.PdfPath),
                    PdfPath = x.r.PdfPath
                });
            }

            foreach (var enrollment in enrollments.Where(e => e.DownPayment > 0))
            {
                var downpaymentDate = ResolveDownpaymentDateText(enrollment, contractCreatedByEnrollment);
                Receipts.Add(new ReceiptRowVm
                {
                    IsDownpayment = true,
                    EnrollmentId = enrollment.EnrollmentId,
                    NumberText = "DP",
                    DateText = downpaymentDate,
                    AmountText = $"{enrollment.DownPayment:0.00} €",
                    MethodText = "Enrollment",
                    ReasonText = "ΠΡΟΚΑΤΑΒΟΛΗ",
                    ProgramText = ProgramLabel(enrollment.ProgramType),
                    HasPdf = false,
                    PdfPath = ""
                });
            }

            var contractRows = await db.Contracts
                .AsNoTracking()
                .Where(c => c.StudentId == _studentId && enrollmentIds.Contains(c.EnrollmentId))
                .Include(c => c.Enrollment)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            foreach (var c in contractRows)
            {
                var isPendingPrint = string.IsNullOrWhiteSpace(c.PdfPath);
                var programText = c.Enrollment is null
                    ? "—"
                    : string.IsNullOrWhiteSpace(c.Enrollment.LevelOrClass)
                        ? c.Enrollment.ProgramType.ToDisplayName()
                        : $"{c.Enrollment.ProgramType} ({c.Enrollment.LevelOrClass})";

                Contracts.Add(new ContractRowVm
                {
                    ContractId = c.ContractId,
                    CreatedAtText = c.CreatedAt.ToString("dd/MM/yyyy"),
                    ProgramText = programText,
                    IsPendingPrint = isPendingPrint,
                    DocxPath = c.DocxPath,
                    PdfPath = c.PdfPath
                });
            }

            var pendingCount = Contracts.Count(c => c.IsPendingPrint);
            PendingContractsText = pendingCount > 0
                ? $"⚠ Pending contracts: {pendingCount} (not printed/signed)"
                : "No pending contracts.";

        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "LoadAsync failed");
        }
        finally
        {
            _isLoading = false;
        }
    }
    private static string BuildInstallmentAmountText(Enrollment e)
    {
        var schedule = InstallmentPlanHelper.GetInstallmentSchedule(e);
        if (schedule.Count == 0)
            return "—";

        var first = schedule[0];
        var last = schedule[schedule.Count - 1];

        if (first == last)
            return $"{first:0} €";

        return $"{first:0} € (last {last:0} €)";
    }



    private static string BuildInstallmentPlanText(Enrollment e)
    {
        if (e.InstallmentStartMonth is null)
            return "—";

        var start = new DateTime(e.InstallmentStartMonth.Value.Year, e.InstallmentStartMonth.Value.Month, 1);
        var day = e.InstallmentDayOfMonth <= 0 ? 1 : Math.Min(e.InstallmentDayOfMonth, DateTime.DaysInMonth(start.Year, start.Month));
        var startDate = new DateTime(start.Year, start.Month, day);
        return $"{e.InstallmentCount} from {startDate:dd/MM/yyyy}";
    }

    private static string ResolveDownpaymentDateText(Enrollment enrollment, IReadOnlyDictionary<Guid, DateTime> contractCreatedByEnrollment)
    {
        if (contractCreatedByEnrollment.TryGetValue(enrollment.EnrollmentId, out var createdAt))
            return createdAt.ToString("dd/MM/yyyy");

        var firstPaymentDate = enrollment.Payments
            .OrderBy(p => p.PaymentDate)
            .Select(p => p.PaymentDate)
            .FirstOrDefault();

        if (firstPaymentDate != default)
            return firstPaymentDate.ToString("dd/MM/yyyy");

        return DateTime.Today.ToString("dd/MM/yyyy");
    }

    private static string ParseReason(string? notes)
    {
        var raw = (notes ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return "—";

        var parts = raw.Split('|', 2, StringSplitOptions.TrimEntries);
        return parts[0];
    }

    private static string ParseAdditionalNotes(string? notes)
    {
        var raw = (notes ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var parts = raw.Split('|', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? parts[1] : "";
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (File.Exists(path))
            File.Delete(path);
    }

    private static (string Name, string Surname) SplitName(string? fullName)
    {
        var value = (fullName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value)) return ("", "");

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return (parts[0], "");

        return (parts[0], string.Join(" ", parts.Skip(1)));
    }

    private static string JoinName(string? name, string? surname)
    {
        return string.Join(" ", new[] { name?.Trim(), surname?.Trim() }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static (string Phone, string Email) SplitPhoneEmail(string? value)
    {
        var raw = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw)) return ("", "");

        var parts = raw.Split('|', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (raw, "");
    }

    private static string JoinPhoneEmail(string? phone, string? email)
    {
        var p = phone?.Trim() ?? "";
        var e = email?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(p)) return e;
        if (string.IsNullOrWhiteSpace(e)) return p;
        return $"{p} | {e}";
    }

}
