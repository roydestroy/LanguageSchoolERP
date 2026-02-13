using CommunityToolkit.Mvvm.ComponentModel;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LanguageSchoolERP.App.Windows;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace LanguageSchoolERP.App.ViewModels;

public partial class StudentProfileViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly DbContextFactory _dbFactory;

    private Guid _studentId;

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
    public StudentProfileViewModel(AppState state, DbContextFactory dbFactory)
    {
        _state = state;
        _dbFactory = dbFactory;

        AddPaymentCommand = new RelayCommand(OpenAddPaymentDialog);
        PrintReceiptCommand = new RelayCommand(PrintSelectedReceipt);

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
    private void PrintSelectedReceipt()
    {
        if (SelectedReceipt is null)
        {
            System.Windows.MessageBox.Show("Please select a receipt first.");
            return;
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

            const decimal downpaymentTolerance = 0.01m;

            static bool HasDownpaymentHint(Payment payment)
            {
                var notes = payment.Notes ?? string.Empty;
                var method = payment.Method.ToString();
                return notes.Contains("downpayment", StringComparison.OrdinalIgnoreCase)
                    || notes.Contains("down payment", StringComparison.OrdinalIgnoreCase)
                    || notes.Contains("enrollment", StringComparison.OrdinalIgnoreCase)
                    || method.Contains("downpayment", StringComparison.OrdinalIgnoreCase)
                    || method.Contains("enrollment", StringComparison.OrdinalIgnoreCase);
            }

            // Find one matching downpayment payment per enrollment (if any).
            var matchedDownpaymentByEnrollment = enrollments
                .Where(e => e.DownPayment > 0)
                .ToDictionary(
                    e => e.EnrollmentId,
                    e => e.Payments
                        .Where(p => Math.Abs(p.Amount - e.DownPayment) <= downpaymentTolerance || HasDownpaymentHint(p))
                        .OrderBy(p => p.PaymentDate)
                        .FirstOrDefault());

            // Summary across enrollments (grouped by caret in list; here we aggregate)
            decimal agreementSum = enrollments.Sum(e => e.AgreementTotal);
            decimal paidSum = enrollments.Sum(e => e.Payments.Sum(p => p.Amount));
            decimal unmatchedDownpaymentSum = enrollments
                .Where(e => e.DownPayment > 0)
                .Sum(e => matchedDownpaymentByEnrollment[e.EnrollmentId] is null ? e.DownPayment : 0m);
            decimal paidTotal = paidSum + unmatchedDownpaymentSum;
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

            var downpaymentPaymentIds = matchedDownpaymentByEnrollment.Values
                .Where(p => p is not null)
                .Select(p => p!.PaymentId)
                .ToHashSet();

            foreach (var enrollment in enrollments.Where(e => e.DownPayment > 0))
            {
                if (matchedDownpaymentByEnrollment[enrollment.EnrollmentId] is not null)
                    continue;

                var firstPaymentDate = enrollment.Payments
                    .OrderBy(p => p.PaymentDate)
                    .Select(p => (DateTime?)p.PaymentDate)
                    .FirstOrDefault();

                Payments.Add(new PaymentRowVm
                {
                    TypeText = "Downpayment",
                    DateText = firstPaymentDate?.ToString("dd/MM/yyyy") ?? "—",
                    AmountText = $"{enrollment.DownPayment:0.00} €",
                    Method = "Enrollment",
                    Notes = "Enrollment downpayment"
                });
            }

            foreach (var row in paymentRows)
            {
                Payments.Add(new PaymentRowVm
                {
                    TypeText = downpaymentPaymentIds.Contains(row.Payment.PaymentId) ? "Downpayment" : "Payment",
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

        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.ToString(), "LoadAsync failed");
        }
    }
}
