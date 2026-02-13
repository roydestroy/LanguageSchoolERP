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

public record EnrollmentOption(Guid EnrollmentId, string Label)
{
    public override string ToString() => Label;
}

public record AddPaymentInit(Guid StudentId, string AcademicYear);

public partial class AddPaymentViewModel : ObservableObject
{
    private readonly DbContextFactory _dbFactory;
    private readonly ReceiptNumberService _receiptNumberService;
    private readonly ExcelReceiptGenerator _excelReceiptGenerator;
    private readonly AppState _state;

    public event EventHandler<bool>? RequestClose;

    public List<EnrollmentOption> EnrollmentOptions { get; } = new();

    [ObservableProperty] private EnrollmentOption? selectedEnrollmentOption;

    public IReadOnlyList<PaymentMethod> PaymentMethods { get; } =
        new[] { PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.BankTransfer, PaymentMethod.IRIS, PaymentMethod.Other };

    [ObservableProperty] private PaymentMethod selectedPaymentMethod = PaymentMethod.Cash;

    [ObservableProperty] private string amountText = "";
    [ObservableProperty] private DateTime? paymentDate = DateTime.Today;
    [ObservableProperty] private string notes = "";

    [ObservableProperty] private string errorMessage = "";

    public IRelayCommand SaveCommand { get; }

    private AddPaymentInit? _init;
    private Guid _academicPeriodId;

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
        ErrorMessage = "";
        Notes = "";
        AmountText = "";
        PaymentDate = DateTime.Today;
        SelectedPaymentMethod = PaymentMethod.Cash;

        EnrollmentOptions.Clear();

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

        _academicPeriodId = period.AcademicPeriodId;

        var enrollments = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == init.StudentId && e.AcademicPeriodId == period.AcademicPeriodId)
            .OrderBy(e => e.ProgramType)
            .ToListAsync();

        foreach (var e in enrollments)
        {
            var label = string.IsNullOrWhiteSpace(e.LevelOrClass)
                ? $"{e.ProgramType}"
                : $"{e.ProgramType} ({e.LevelOrClass})";

            EnrollmentOptions.Add(new EnrollmentOption(e.EnrollmentId, label));
        }

        SelectedEnrollmentOption = EnrollmentOptions.FirstOrDefault();
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
            // 1) Get receipt number first (atomic per academic year)
            var receiptNumber = await _receiptNumberService.GetNextReceiptNumberAsync(_academicPeriodId);

            using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            // 2) Create payment
            var payment = new Payment
            {
                EnrollmentId = SelectedEnrollmentOption.EnrollmentId,
                PaymentDate = PaymentDate.Value.Date,
                Amount = amount,
                Method = SelectedPaymentMethod,
                Notes = Notes?.Trim() ?? ""
            };

            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            // 3) Load needed data for PDF path + print info
            var enrollment = await db.Enrollments
                .AsNoTracking()
                .Include(e => e.Student)
                .Include(e => e.AcademicPeriod)
                .FirstAsync(e => e.EnrollmentId == SelectedEnrollmentOption.EnrollmentId);

            var student = enrollment.Student;
            var academicYear = enrollment.AcademicPeriod.Name;

            // 4) Build PDF path
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var receiptsRoot = Path.Combine(baseDir, "Receipts");

            var studentFolder = ReceiptPathService.GetStudentFolder(
                baseDir: receiptsRoot,
                dbName: _state.SelectedDatabaseName,
                academicYear: academicYear,
                studentFullName: student.FullName
            );

            var pdfPath = ReceiptPathService.GetReceiptPdfPath(studentFolder, receiptNumber);

            // 5) Generate PDF from Excel template
            var templatePath = ReceiptTemplateResolver.GetTemplatePath(_state.SelectedDatabaseName);

            var data = new ReceiptPrintData(
                ReceiptNumber: receiptNumber,
                IssueDate: payment.PaymentDate,
                StudentName: student.FullName,
                StudentPhone: student.Phone ?? "",
                StudentEmail: student.Email ?? "",
                Amount: payment.Amount,
                PaymentMethod: payment.Method.ToString(),
                ProgramLabel: enrollment.ProgramType.ToString(),
                AcademicYear: academicYear,
                Notes: payment.Notes ?? ""
            );

            _excelReceiptGenerator.GenerateReceiptPdf(templatePath, pdfPath, data);

            // 6) Create receipt row
            var receipt = new Receipt
            {
                PaymentId = payment.PaymentId,
                ReceiptNumber = receiptNumber,
                IssueDate = payment.PaymentDate,
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
