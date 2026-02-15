using System.Globalization;
using LanguageSchoolERP.Core.Models;

namespace LanguageSchoolERP.Services;

public sealed class ContractBookmarkBuilder
{
    private static readonly CultureInfo GreekCulture = new("el-GR");

    public Dictionary<string, string> BuildBookmarkValues(ContractPayload payload, Enrollment enrollment)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cur1"] = payload.CreatedAt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["cur"] = BuildGreekLongDate(payload.CreatedAt),
            ["gen"] = InferFemale(payload.GuardianFullName) ? "η" : "ο",
            ["gen1"] = InferFemale(payload.GuardianFullName) ? "υπογεγραμμένη" : "υπογεγραμμένος",
            ["tou"] = InferFemale(payload.StudentFullName) ? "της" : "του",
            ["on_up"] = payload.GuardianFullName,
            ["on_sp"] = ToGenitiveFullName(payload.StudentFullName),
            ["per_prg"] = payload.ProgramNameUpper,
            ["tit_prg"] = payload.ProgramTitleUpperWithExtras,
            ["sun_pos"] = FormatPlainAmount(payload.AgreementTotal),
            ["prok_pos"] = FormatPlainAmount(payload.DownPayment)
        };

        if (payload.IncludesTransportation)
        {
            values["slab"] = "\r\nΓια την μεταφορά από το Σχολείο προς το Φροντιστήριο, το συμφωνηθέν ποσό καθορίζεται ως εξής:";
            values["slabc"] = $"{(payload.TransportationMonthlyPrice ?? 0m):0.##}€/μήνα";
        }
        else if (payload.IncludesStudyLab)
        {
            values["slab"] = "\r\nΓια την συμμετοχή στο πρόγραμμα Study Lab το συμφωνηθέν ποσό καθορίζεται ως εξής:";
            values["slabc"] = $"{(payload.StudyLabMonthlyPrice ?? 0m):0.##}€/μήνα";
        }
        else
        {
            values["slab"] = "";
            values["slabc"] = "";
        }

        BuildInstallments(values, enrollment);
        return values;
    }

    public static string BuildProgramTitleUpperWithExtras(Enrollment enrollment)
    {
        var baseTitle = enrollment.ProgramType switch
        {
            ProgramType.LanguageSchool => $"{enrollment.ProgramType} {enrollment.LevelOrClass}".Trim(),
            _ => string.IsNullOrWhiteSpace(enrollment.LevelOrClass)
                ? enrollment.ProgramType.ToString()
                : $"{enrollment.ProgramType} {enrollment.LevelOrClass}".Trim()
        };

        if (enrollment.IncludesTransportation)
            return $"{baseTitle} + ΜΕΤΑΦΟΡΑ".ToUpperInvariant();
        if (enrollment.IncludesStudyLab)
            return $"{baseTitle} + STUDY LAB".ToUpperInvariant();

        return baseTitle.ToUpperInvariant();
    }

    private static void BuildInstallments(Dictionary<string, string> values, Enrollment enrollment)
    {
        for (var i = 1; i <= 12; i++)
        {
            values[$"aa{i}"] = "";
            values[$"dat{i}"] = "";
            values[$"dos{i}"] = "";
        }

        var financed = enrollment.AgreementTotal - enrollment.DownPayment;
        if (enrollment.InstallmentCount <= 0 || financed <= 0 || enrollment.InstallmentStartMonth is null)
            return;

        var schedule = InstallmentPlanHelper.GetInstallmentSchedule(enrollment);
        var start = new DateTime(enrollment.InstallmentStartMonth.Value.Year, enrollment.InstallmentStartMonth.Value.Month, 1);
        var day = enrollment.InstallmentDayOfMonth <= 0 ? 1 : enrollment.InstallmentDayOfMonth;

        for (var i = 0; i < schedule.Count; i++)
        {
            var month = start.AddMonths(i);
            var clampedDay = Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month));
            var date = new DateTime(month.Year, month.Month, clampedDay);
            var index = i + 1;

            values[$"aa{index}"] = $"{index}η Δόση";
            values[$"dat{index}"] = date.ToString("d/M/yyyy", CultureInfo.InvariantCulture);
            values[$"dos{index}"] = $"{schedule[i]:0.##}€";
        }
    }

    private static string BuildGreekLongDate(DateTime date)
    {
        var dayName = GreekCulture.DateTimeFormat.GetDayName(date.DayOfWeek);
        var monthName = GreekCulture.DateTimeFormat.MonthGenitiveNames[date.Month - 1];
        return $"{CapitalizeFirst(dayName)}, {date.Day} {monthName} {date.Year}";
    }

    private static string CapitalizeFirst(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return char.ToUpper(value[0], GreekCulture) + value[1..];
    }

    private static string FormatPlainAmount(decimal amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);

    private static bool InferFemale(string fullName)
    {
        var first = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return first.EndsWith("α", StringComparison.OrdinalIgnoreCase)
            || first.EndsWith("η", StringComparison.OrdinalIgnoreCase)
            || first.EndsWith("ω", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToGenitiveFullName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return fullName;

        parts[0] = ToGenitiveGivenName(parts[0]);
        return string.Join(" ", parts);
    }

    private static string ToGenitiveGivenName(string name)
    {
        if (name.EndsWith("ος", StringComparison.OrdinalIgnoreCase))
            return name[..^2] + "ου";
        if (name.EndsWith("ας", StringComparison.OrdinalIgnoreCase))
            return name[..^2] + "α";
        if (name.EndsWith("ης", StringComparison.OrdinalIgnoreCase))
            return name[..^2] + "η";

        return name;
    }
}
