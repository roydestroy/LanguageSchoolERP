using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanguageSchoolERP.App.Extensions;
using LanguageSchoolERP.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;

namespace LanguageSchoolERP.App.ViewModels;

public partial class DailyPaymentsViewModel : ObservableObject
{
    private readonly DbContextFactory _dbFactory;
    private readonly DailyPaymentsReportService _reportService;

    public ObservableCollection<DailyPaymentRowVm> Payments { get; } = new();

    [ObservableProperty] private DateTime selectedDate = DateTime.Today;
    [ObservableProperty] private string totalAmountText = "0,00 €";
    [ObservableProperty] private string errorMessage = string.Empty;

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand PrintCommand { get; }

    public DailyPaymentsViewModel(DbContextFactory dbFactory, DailyPaymentsReportService reportService)
    {
        _dbFactory = dbFactory;
        _reportService = reportService;

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        PrintCommand = new AsyncRelayCommand(PrintAsync, CanPrint);
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            await using var db = _dbFactory.Create();
            DbSeeder.EnsureSeeded(db);

            var dayStart = SelectedDate.Date;
            var dayEnd = dayStart.AddDays(1);

            var payments = await db.Payments
                .AsNoTracking()
                .Where(p => p.PaymentDate >= dayStart && p.PaymentDate < dayEnd)
                .Include(p => p.Enrollment)
                    .ThenInclude(e => e.Student)
                .Include(p => p.Enrollment)
                    .ThenInclude(e => e.Program)
                .OrderBy(p => p.PaymentDate)
                .ToListAsync();

            Payments.Clear();
            foreach (var payment in payments)
            {
                Payments.Add(new DailyPaymentRowVm
                {
                    TimeText = payment.PaymentDate.ToString("HH:mm"),
                    StudentName = payment.Enrollment.Student.FullName,
                    ProgramName = payment.Enrollment.Program.Name,
                    Amount = payment.Amount,
                    AmountText = $"{payment.Amount:0.00} €",
                    MethodText = payment.Method.ToGreekLabel(),
                    Notes = payment.Notes
                });
            }

            var total = Payments.Sum(x => x.Amount);
            TotalAmountText = $"{total:0.00} €";
            PrintCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Αποτυχία φόρτωσης πληρωμών: {ex.Message}";
            Payments.Clear();
            TotalAmountText = "0,00 €";
            PrintCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanPrint() => Payments.Count > 0;

    private async Task PrintAsync()
    {
        if (!CanPrint())
            return;

        var saveDialog = new SaveFileDialog
        {
            FileName = $"Πληρωμές_{SelectedDate:yyyyMMdd}",
            DefaultExt = ".pdf",
            Filter = "PDF files (*.pdf)|*.pdf"
        };

        if (saveDialog.ShowDialog() != true)
            return;

        try
        {
            var items = Payments.Select(p => new DailyPaymentsReportItem
            {
                Time = p.TimeText,
                StudentName = p.StudentName,
                ProgramName = p.ProgramName,
                Amount = p.Amount,
                Method = p.MethodText,
                Notes = p.Notes
            }).ToList();

            _reportService.GenerateDailyPaymentsPdf(saveDialog.FileName, SelectedDate, items);
            await Task.CompletedTask;

            MessageBox.Show("Το αρχείο PDF δημιουργήθηκε επιτυχώς.", "Επιτυχία", MessageBoxButton.OK, MessageBoxImage.Information);

            Process.Start(new ProcessStartInfo
            {
                FileName = saveDialog.FileName,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Αποτυχία δημιουργίας PDF: {ex.Message}", "Σφάλμα", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public sealed class DailyPaymentRowVm
{
    public string TimeText { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ProgramName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string AmountText { get; set; } = string.Empty;
    public string MethodText { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
