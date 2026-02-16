using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.Core.Models;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LanguageSchoolERP.App.ViewModels;

public record EnrollmentOption(Guid EnrollmentId, string Label, decimal SuggestedAmount)
{
    public override string ToString() => Label;
}

public record AddPaymentInit(Guid StudentId, string AcademicYear, Guid? PaymentId = null);

public partial class AddPaymentViewModel : ObservableObject
{
    private readonly DbContextFactory _dbFactory;
    private readonly ReceiptNumberService _receiptNumberService;
    private readonly ExcelReceiptGenerator _excelReceiptGenerator;
    private readonly AppState _state;

    public event EventHandler<bool>? RequestClose;

    public List<EnrollmentOption> EnrollmentOptions { get; } = new();

    [ObservableProperty] private EnrollmentOption? selectedEnrollmentOption;
    [ObservableProperty] private string dialogTitle = "Add Payment";

    public IReadOnlyList<PaymentMethod> PaymentMethods { get; } =
        new[] { PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.BankTransfer, PaymentMethod.IRIS, PaymentMethod.Other };

    [ObservableProperty] private PaymentMethod selectedPaymentMethod = PaymentMethod.Cash;

    public IReadOnlyList<string> PaymentReasons { get; } =
    [
        "ΙΑΝΟΥΑΡΙΟΣ",
        "ΦΕΒΡΟΥΑΡΙΟΣ",
        "ΜΑΡΤΙΟΣ",
        "ΑΠΡΙΛΙΟΣ",
        "ΜΑΪΟΣ",
        "ΙΟΥΝΙΟΣ",
        "ΙΟΥΛΙΟΣ",
        "ΑΥΓΟΥΣΤΟΣ",
        "ΣΕΠΤΕΜΒΡΙΟΣ",
        "ΟΚΤΩΒΡΙΟΣ",
        "ΝΟΕΜΒΡΙΟΣ",
        "ΔΕΚΕΜΒΡΙΟΣ",
        "ΠΛΗΡΟΦΟΡΙΚΗ",
        "ΠΡΟΚΑΤΑΒΟΛΗ",
        "ΒΙΒΛΙΑ",
        "ΕΞΕΤΑΣΤΡΑ",
        "ΕΝΑΝΤΙ",
        "ΕΞΟΦΛΗΣΗ",
        "ΔΙΔΑΚΤΡΑ"
    ];

    [ObservableProperty] private string selectedReason = "ΔΙΔΑΚΤΡΑ";

    [ObservableProperty] private string amountText = "";
    [ObservableProperty] private DateTime? paymentDate = DateTime.Today;
    [ObservableProperty] private string notes = "";

    [ObservableProperty] private string errorMessage = "";

    public IRelayCommand SaveCommand { get; }

    private AddPaymentInit? _init;
    private readonly Dictionary<Guid, decimal> _suggestedAmountsByEnrollment = new();
    private bool _isEditMode;

    public AddPaymentViewModel(
        DbContextFactory dbFactory,
        ReceiptNumberService receiptNumberService,
        ExcelReceiptGenerator excelReceiptGenerator,
        AppState state)
    {
        _dbFactory = dbFactory;
        _receiptNumberService = receiptNumberService;
        _excelReceiptGenerator = excelReceiptGenerator;
        _state = state;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public async void Initialize(AddPaymentInit init)
    {
        _init = init;
        _isEditMode = init.PaymentId.HasValue;
        DialogTitle = _isEditMode ? "Edit Payment" : "Add Payment";

        ErrorMessage = "";
        Notes = "";
        AmountText = "";
        PaymentDate = DateTime.Today;
        SelectedPaymentMethod = PaymentMethod.Cash;
        SelectedReason = "ΔΙΔΑΚΤΡΑ";

        EnrollmentOptions.Clear();
        _suggestedAmountsByEnrollment.Clear();

        using var db = _dbFactory.Create();
        DbSeeder.EnsureSeeded(db);

        var period = await db.AcademicPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == init.AcademicYear);

        if (period is null)
        {
            ErrorMessage = $"Academic year '{init.AcademicYear}' not found.";
            return;
        }

        var enrollments = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == init.StudentId && e.AcademicPeriodId == period.AcademicPeriodId)
            .Include(e => e.Payments)
            .OrderBy(e => e.ProgramType)
            .ToListAsync();

        foreach (var e in enrollments)
        {
            var label = string.IsNullOrWhiteSpace(e.LevelOrClass)
                ? $"{e.ProgramType.ToDisplayName()}"
                : $"{e.ProgramType.ToDisplayName()} ({e.LevelOrClass})";

            var suggested = InstallmentPlanHelper.GetNextInstallmentAmount(e);
            _suggestedAmountsByEnrollment[e.EnrollmentId] = suggested;

            var optionLabel = suggested > 0
                ? $"{label} - next: {suggested:0} €"
                : label;

            EnrollmentOptions.Add(new EnrollmentOption(e.EnrollmentId, optionLabel, suggested));
        }

        if (_isEditMode)
        {
            var payment = await db.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PaymentId == init.PaymentId.Value);

            if (payment is null)
            {
                ErrorMessage = "Payment not found.";
                return;
            }

            SelectedEnrollmentOption = EnrollmentOptions.FirstOrDefault(o => o.EnrollmentId == payment.EnrollmentId)
                                      ?? EnrollmentOptions.FirstOrDefault();
            AmountText = payment.Amount.ToString("0.##", CultureInfo.InvariantCulture);
            PaymentDate = payment.PaymentDate.Date;
            SelectedPaymentMethod = payment.Method;
            SelectedReason = ParseReason(payment.Notes);
            Notes = ParseAdditionalNotes(payment.Notes);
        }
        else
        {
            SelectedEnrollmentOption = EnrollmentOptions.FirstOrDefault();
        }
    }

    partial void OnSelectedEnrollmentOptionChanged(EnrollmentOption? value)
    {
        if (_isEditMode || value is null)
        {
            return;
        }

        if (_suggestedAmountsByEnrollment.TryGetValue(value.EnrollmentId, out var suggested) && suggested > 0)
        {
            AmountText = suggested.ToString("0", CultureInfo.InvariantCulture);
        }
    }

    private async Task SaveAsync()
    {
        ErrorMessage = "";

        if (_init is null)
        {
            ErrorMessage = "Dialog not initialized.";
            return;
        }

        if (SelectedEnrollmentOption is null)
        {
            ErrorMessage = "Please select an enrollment.";
            return;
        }

        if (!TryParseMoney(AmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Amount must be a valid number (> 0).";
            return;
        }

        if (PaymentDate is null)
        {
            ErrorMessage = "Please select a payment date.";
            return;
        }

        try
        {
            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            if (_isEditMode && _init.PaymentId.HasValue)
            {
                var payment = await db.Payments
                    .FirstOrDefaultAsync(p => p.PaymentId == _init.PaymentId.Value);

                if (payment is null)
                {
                    ErrorMessage = "Payment not found.";
                    return;
                }

                payment.EnrollmentId = SelectedEnrollmentOption.EnrollmentId;
                payment.PaymentDate = PaymentDate.Value.Date;
                payment.Amount = amount;
                payment.Method = SelectedPaymentMethod;
                payment.Notes = BuildStoredNotes(SelectedReason, Notes);

                var receipts = await db.Receipts
                    .Where(r => r.PaymentId == payment.PaymentId)
                    .ToListAsync();

                foreach (var r in receipts)
                {
                    r.IssueDate = payment.PaymentDate;
                }

                await db.SaveChangesAsync();
                RequestClose?.Invoke(this, true);
                return;
            }

            // Create mode: generate payment + receipt
            var receiptNumber = await _receiptNumberService.GetNextReceiptNumberAsync(SelectedEnrollmentOption.EnrollmentId);

            var newPayment = new Payment
            {
                EnrollmentId = SelectedEnrollmentOption.EnrollmentId,
                PaymentDate = PaymentDate.Value.Date,
                Amount = amount,
                Method = SelectedPaymentMethod,
                Notes = BuildStoredNotes(SelectedReason, Notes)
            };

            db.Payments.Add(newPayment);
            await db.SaveChangesAsync();

            var enrollment = await db.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Include(e => e.AcademicPeriod)
                .FirstAsync(e => e.EnrollmentId == SelectedEnrollmentOption.EnrollmentId);

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

            var pdfPath = ReceiptPathService.GetReceiptPdfPath(studentFolder, receiptNumber);
            var templatePath = ReceiptTemplateResolver.GetTemplatePath(_state.SelectedDatabaseName);

            var data = new ReceiptPrintData(
                ReceiptNumber: receiptNumber,
                IssueDate: newPayment.PaymentDate,
                StudentName: student.FullName,
                StudentPhone: student.Phone ?? "",
                StudentEmail: student.Email ?? "",
                Amount: newPayment.Amount,
                PaymentMethod: newPayment.Method.ToString(),
                ProgramLabel: enrollment.ProgramType.ToDisplayName(),
                AcademicYear: academicYear,
                Notes: newPayment.Notes ?? ""
            );

            _excelReceiptGenerator.GenerateReceiptPdf(templatePath, pdfPath, data);

            var receipt = new Receipt
            {
                PaymentId = newPayment.PaymentId,
                ReceiptNumber = receiptNumber,
                IssueDate = newPayment.PaymentDate,
                PdfPath = pdfPath,
                Voided = false,
                VoidReason = ""
            };

            db.Receipts.Add(receipt);
            await db.SaveChangesAsync();

            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private static string ParseReason(string? notes)
    {
        var raw = (notes ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return "ΔΙΔΑΚΤΡΑ";

        var pipe = raw.IndexOf('|');
        if (pipe < 0)
            return raw;

        return raw[..pipe].Trim();
    }

    private static string ParseAdditionalNotes(string? notes)
    {
        var raw = (notes ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var pipe = raw.IndexOf('|');
        if (pipe < 0)
            return "";

        return raw[(pipe + 1)..].Trim();
    }

    private static string BuildStoredNotes(string reason, string? notes)
    {
        var safeReason = (reason ?? "").Trim();
        var safeNotes = (notes ?? "").Trim();

        if (string.IsNullOrWhiteSpace(safeReason))
            return safeNotes;

        if (string.IsNullOrWhiteSpace(safeNotes))
            return safeReason;

        return $"{safeReason} | {safeNotes}";
    }

    private static bool TryParseMoney(string text, out decimal value)
    {
        text = (text ?? "").Trim();

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;

        if (decimal.TryParse(text, NumberStyles.Number, new CultureInfo("el-GR"), out value))
            return true;

        value = 0;
        return false;
    }
}
