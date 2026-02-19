using System;
using System.Collections.Generic;
using LanguageSchoolERP.Core.Models;

namespace LanguageSchoolERP.Services;

public static class InstallmentPlanHelper
{
    public static bool IsEnrollmentOverdue(Enrollment e, DateTime today)
    {
        if (e.IsStopped)
            return false;

        if (e.InstallmentCount <= 0 || e.InstallmentStartMonth is null)
            return false;

        var start = new DateTime(e.InstallmentStartMonth.Value.Year, e.InstallmentStartMonth.Value.Month, 1);
        if (today < start)
            return false;

        var paid = e.DownPayment + SumPayments(e);
        var remaining = GetEffectiveAgreementTotal(e) - paid;
        if (remaining <= 0)
            return false;

        int monthsElapsed = MonthsBetweenInclusive(start, new DateTime(today.Year, today.Month, 1));
        int installmentsDue = Math.Min(e.InstallmentCount, monthsElapsed);
        if (installmentsDue <= 0)
            return false;

        var schedule = GetInstallmentSchedule(e);
        decimal expectedPaid = e.DownPayment;
        for (int i = 0; i < installmentsDue && i < schedule.Count; i++)
            expectedPaid += schedule[i];

        return paid + 0.009m < expectedPaid;
    }

    public static IReadOnlyList<decimal> GetInstallmentSchedule(Enrollment e)
    {
        var result = new List<decimal>();

        if (e.InstallmentCount <= 0)
            return result;

        var financedAmount = GetRoundedFinancedAmount(e);
        if (financedAmount <= 0)
            return result;

        var baseAmount = Math.Floor(financedAmount / e.InstallmentCount);

        for (int i = 0; i < e.InstallmentCount - 1; i++)
            result.Add(baseAmount);

        var used = baseAmount * Math.Max(0, e.InstallmentCount - 1);
        var lastAmount = financedAmount - used;
        result.Add(lastAmount);

        return result;
    }

    public static decimal GetRegularInstallmentAmount(Enrollment e)
    {
        var schedule = GetInstallmentSchedule(e);
        return schedule.Count == 0 ? 0m : schedule[0];
    }

    public static decimal GetNextInstallmentAmount(Enrollment e)
    {
        if (e.IsStopped)
            return 0m;

        if (e.InstallmentCount <= 0)
        {
            var remaining = GetEffectiveAgreementTotal(e) - (e.DownPayment + SumPayments(e));
            return remaining > 0 ? remaining : 0m;
        }

        var schedule = GetInstallmentSchedule(e);
        if (schedule.Count == 0)
            return 0m;

        var paidTowardInstallments = SumPayments(e);

        foreach (var installment in schedule)
        {
            if (paidTowardInstallments >= installment)
            {
                paidTowardInstallments -= installment;
                continue;
            }

            return installment - paidTowardInstallments;
        }

        return 0m;
    }


    public static decimal GetEffectiveAgreementTotal(Enrollment e)
    {
        var studyLabTotal = e.IncludesStudyLab ? (e.StudyLabMonthlyPrice ?? 0m) : 0m;
        return e.AgreementTotal + studyLabTotal;
    }

    public static decimal GetOutstandingAmount(Enrollment e)
    {
        var paid = e.DownPayment + SumPayments(e);
        var remaining = GetEffectiveAgreementTotal(e) - paid;
        return remaining > 0 ? remaining : 0m;
    }

    public static decimal GetLostAmount(Enrollment e)
    {
        if (!e.IsStopped)
            return 0m;

        if (e.StoppedAmountWaived > 0)
            return e.StoppedAmountWaived;

        var paid = e.DownPayment + SumPayments(e);
        var remaining = GetEffectiveAgreementTotal(e) - paid;
        return remaining > 0 ? remaining : 0m;
    }

    private static decimal GetRoundedFinancedAmount(Enrollment e)
    {
        var financed = GetEffectiveAgreementTotal(e) - e.DownPayment;
        if (financed <= 0)
            return 0m;

        return Math.Round(financed, 0, MidpointRounding.AwayFromZero);
    }

    private static decimal SumPayments(Enrollment e)
    {
        return PaymentAgreementHelper.SumAgreementPayments(e.Payments);
    }

    private static int MonthsBetweenInclusive(DateTime startMonth, DateTime endMonth)
    {
        int months = (endMonth.Year - startMonth.Year) * 12 + (endMonth.Month - startMonth.Month) + 1;
        return Math.Max(0, months);
    }
}
