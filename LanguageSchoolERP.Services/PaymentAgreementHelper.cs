using System;
using System.Collections.Generic;
using LanguageSchoolERP.Core.Models;

namespace LanguageSchoolERP.Services;

public static class PaymentAgreementHelper
{
    public const string ExcludeFromAgreementMarker = "[ΕΚΤΟΣ_ΣΥΜΦΩΝΗΘΕΝΤΟΣ]";
    private const string LegacyExcludeFromAgreementMarker = "[EXCLUDE_FROM_AGREEMENT]";

    public const string ExcludeFromAgreementDisplayText = "Εκτός συμφωνηθέντος ποσού";

    public static bool IsExcludedFromAgreement(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return false;

        return notes.Contains(ExcludeFromAgreementMarker, StringComparison.OrdinalIgnoreCase)
               || notes.Contains(LegacyExcludeFromAgreementMarker, StringComparison.OrdinalIgnoreCase);
    }

    public static string AddExcludeMarker(string notes)
    {
        var safeNotes = (notes ?? string.Empty).Trim();

        if (IsExcludedFromAgreement(safeNotes))
            return safeNotes;

        return string.IsNullOrWhiteSpace(safeNotes)
            ? ExcludeFromAgreementMarker
            : $"{safeNotes} | {ExcludeFromAgreementMarker}";
    }

    public static string RemoveExcludeMarker(string? notes)
    {
        var safeNotes = (notes ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(safeNotes))
            return string.Empty;

        var cleaned = safeNotes
            .Replace(ExcludeFromAgreementMarker, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(LegacyExcludeFromAgreementMarker, string.Empty, StringComparison.OrdinalIgnoreCase);
        cleaned = cleaned.Replace("||", "|");
        cleaned = cleaned.Trim().Trim('|').Trim();

        return cleaned;
    }


    public static string BuildDisplayNotes(string? notes)
    {
        var cleaned = RemoveExcludeMarker(notes);
        if (!IsExcludedFromAgreement(notes))
            return cleaned;

        return string.IsNullOrWhiteSpace(cleaned)
            ? ExcludeFromAgreementDisplayText
            : $"{cleaned} | {ExcludeFromAgreementDisplayText}";
    }

    public static decimal SumAgreementPayments(IEnumerable<Payment>? payments)
    {
        if (payments is null)
            return 0m;

        decimal sum = 0m;
        foreach (var payment in payments)
        {
            if (IsExcludedFromAgreement(payment.Notes))
                continue;

            sum += payment.Amount;
        }

        return sum;
    }
}
