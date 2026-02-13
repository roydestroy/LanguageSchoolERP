using System;
using System.Collections.Generic;
using LanguageSchoolERP.Core.Models;

namespace LanguageSchoolERP.Services;

public static class InstallmentPlanHelper
{
    public static bool IsEnrollmentOverdue(Enrollment e, DateTime today)
    {
        if (e.InstallmentCount <= 0 || e.InstallmentStartMonth is null)
            return false;

        // Normalize start month to first day
        var start = new DateTime(e.InstallmentStartMonth.Value.Year, e.InstallmentStartMonth.Value.Month, 1);

        // If plan starts in the future, not overdue
        if (today < start)
            return false;

        var agreement = e.AgreementTotal;
        var paid = e.DownPayment + SumPayments(e);

        var remaining = agreement - paid;
        if (remaining <= 0)
            return false;

        // installments elapsed (inclusive of start month)
        int monthsElapsed = MonthsBetweenInclusive(start, new DateTime(today.Year, today.Month, 1));
        int installmentsDue = Math.Min(e.InstallmentCount, monthsElapsed);

        if (installmentsDue <= 0)
            return false;

        decimal expectedPaid = ExpectedPaidSoFar(agreement, e.InstallmentCount, installmentsDue);

        return paid + 0.009m < expectedPaid; // tiny tolerance for rounding
    }

    public static decimal ExpectedPaidSoFar(decimal agreementTotal, int installmentCount, int installmentsDue)
    {
        if (installmentCount <= 0 || installmentsDue <= 0) return 0m;

        // base installment
        var baseAmount = Math.Round(agreementTotal / installmentCount, 2, MidpointRounding.AwayFromZero);

        // remainder goes to last installment
        var expected = baseAmount * installmentsDue;

        // If we’ve reached the last installment, expected is the full agreement
        if (installmentsDue >= installmentCount)
            expected = agreementTotal;

        // Cap to agreement
        if (expected > agreementTotal) expected = agreementTotal;

        return expected;
    }


    public static decimal GetRegularInstallmentAmount(decimal agreementTotal, int installmentCount)
    {
        if (installmentCount <= 0 || agreementTotal <= 0)
            return 0m;

        return Math.Round(agreementTotal / installmentCount, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal GetNextInstallmentAmount(Enrollment e)
    {
        var paid = e.DownPayment + SumPayments(e);
        var remaining = e.AgreementTotal - paid;
        if (remaining < 0) remaining = 0;

        if (remaining == 0)
            return 0m;

        if (e.InstallmentCount <= 0)
            return remaining;

        var regular = GetRegularInstallmentAmount(e.AgreementTotal, e.InstallmentCount);
        if (regular <= 0)
            return remaining;

        return remaining < regular ? remaining : regular;
    }

    private static decimal SumPayments(Enrollment e)
    {
        decimal sum = 0;
        if (e.Payments != null)
        {
            foreach (var p in e.Payments)
                sum += p.Amount;
        }
        return sum;
    }

    private static int MonthsBetweenInclusive(DateTime startMonth, DateTime endMonth)
    {
        // both should be first day of month
        int months = (endMonth.Year - startMonth.Year) * 12 + (endMonth.Month - startMonth.Month) + 1;
        return Math.Max(0, months);
    }
}
