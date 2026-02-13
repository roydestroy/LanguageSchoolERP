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

    private Guid _studentId;
    private bool _isLoading;

    public ObservableCollection<string> AvailableAcademicYears { get; } = new();
    public ObservableCollection<PaymentRowVm> Payments { get; } = new();
    public ObservableCollection<ReceiptRowVm> Receipts { get; } = new();
    [ObservableProperty] private ReceiptRowVm? selectedReceipt;
    [ObservableProperty] private string localAcademicYear = "";
    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private string contactLine = "";
    [ObservableProperty] private string notes = "";

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
    public IRelayCommand AddPaymentCommand { get; }
    public IRelayCommand PrintReceiptCommand { get; }
    public StudentProfileViewModel(
        AppState state,
        DbContextFactory dbFactory,
        ExcelReceiptGenerator excelReceiptGenerator)
    {
        _state = state;
        _dbFactory = dbFactory;
        _excelReceiptGenerator = excelReceiptGenerator;

        AddPaymentCommand = new RelayCommand(OpenAddPaymentDialog);
        PrintReceiptCommand = new RelayCommand(() => _ = PrintSelectedReceiptAsync());

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
                    ProgramLabel: enrollment.ProgramType.ToString(),
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

            var period = await db.AcademicPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name == LocalAcademicYear);

            if (period is null) return;

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

            DobLine = student.DateOfBirth.HasValue ? $"DOB: {student.DateOfBirth:dd/MM/yyyy}" : "DOB: —";
            PhoneLine = string.IsNullOrWhiteSpace(student.Phone) ? "Phone: —" : $"Phone: {student.Phone}";
            EmailLine = string.IsNullOrWhiteSpace(student.Email) ? "Email: —" : $"Email: {student.Email}";
            FatherLine = $"Father: {student.FatherName}  ({student.FatherContact})".Trim();
            MotherLine = $"Mother: {student.MotherName}  ({student.MotherContact})".Trim();

            var enrollments = student.Enrollments.ToList();
            static string ProgramLabel(ProgramType p) => p switch
            {
                ProgramType.LanguageSchool => "School",
                ProgramType.StudyLab => "Study Lab",
                ProgramType.EuroLab => "EUROLAB",
                _ => p.ToString()
            };

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
                .Select(e => $"{ProgramLabel(e.ProgramType)}: {e.InstallmentCount} from {e.InstallmentStartMonth:MM/yyyy}")
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

            // Payments table (all payments in this year across enrollments)
            var paymentRows = enrollments
                .SelectMany(e => e.Payments.Select(p => new { Payment = p }))
                .OrderByDescending(x => x.Payment.PaymentDate)
                .ToList();

            foreach (var enrollment in enrollments.Where(e => e.DownPayment > 0))
            {
                Payments.Add(new PaymentRowVm
                {
                    TypeText = "Downpayment",
                    DateText = "—",
                    AmountText = $"{enrollment.DownPayment:0.00} €",
                    Method = "Enrollment",
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
                    Notes = row.Payment.Notes ?? ""
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
                    ProgramText = ProgramLabel(x.e.ProgramType),
                    HasPdf = !string.IsNullOrWhiteSpace(x.r.PdfPath),
                    PdfPath = x.r.PdfPath
                });
            }

            foreach (var enrollment in enrollments.Where(e => e.DownPayment > 0))
            {
                Receipts.Add(new ReceiptRowVm
                {
                    IsDownpayment = true,
                    EnrollmentId = enrollment.EnrollmentId,
                    NumberText = "DP",
                    DateText = "—",
                    AmountText = $"{enrollment.DownPayment:0.00} €",
                    MethodText = "Enrollment",
                    ProgramText = ProgramLabel(enrollment.ProgramType),
                    HasPdf = false,
                    PdfPath = ""
                });
            }

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
}
