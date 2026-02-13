using System;

namespace LanguageSchoolERP.Services;

public static class GreekMoneyTextService
{
    private static readonly string[] Units =
    {
        "", "ΕΝΑ", "ΔΥΟ", "ΤΡΙΑ", "ΤΕΣΣΕΡΑ",
        "ΠΕΝΤΕ", "ΕΞΙ", "ΕΠΤΑ", "ΟΚΤΩ", "ΕΝΝΕΑ",
        "ΔΕΚΑ", "ΕΝΤΕΚΑ", "ΔΩΔΕΚΑ", "ΔΕΚΑΤΡΙΑ",
        "ΔΕΚΑΤΕΣΣΕΡΑ", "ΔΕΚΑΠΕΝΤΕ", "ΔΕΚΑΕΞΙ",
        "ΔΕΚΑΕΠΤΑ", "ΔΕΚΑΟΚΤΩ", "ΔΕΚΑΕΝΝΕΑ"
    };

    private static readonly string[] Tens =
    {
        "", "", "ΕΙΚΟΣΙ", "ΤΡΙΑΝΤΑ", "ΣΑΡΑΝΤΑ",
        "ΠΕΝΗΝΤΑ", "ΕΞΗΝΤΑ", "ΕΒΔΟΜΗΝΤΑ",
        "ΟΓΔΟΝΤΑ", "ΕΝΕΝΗΝΤΑ"
    };

    private static readonly string[] Hundreds =
    {
        "", "ΕΚΑΤΟΝ", "ΔΙΑΚΟΣΙΑ", "ΤΡΙΑΚΟΣΙΑ",
        "ΤΕΤΡΑΚΟΣΙΑ", "ΠΕΝΤΑΚΟΣΙΑ",
        "ΕΞΑΚΟΣΙΑ", "ΕΠΤΑΚΟΣΙΑ",
        "ΟΚΤΑΚΟΣΙΑ", "ΕΝΝΙΑΚΟΣΙΑ"
    };

    public static string AmountToGreekText(decimal amount)
    {
        int euros = (int)Math.Floor(amount);
        int cents = (int)((amount - euros) * 100);

        string euroText = NumberToWords(euros);

        if (euros == 0)
            euroText = "ΜΗΔΕΝ";

        string result = $"{euroText} ΕΥΡΩ";

        if (cents > 0)
            result += $" ΚΑΙ {NumberToWords(cents)} ΛΕΠΤΑ";

        return result;
    }

    private static string NumberToWords(int number)
    {
        if (number == 0)
            return "ΜΗΔΕΝ";

        if (number < 20)
            return Units[number];

        if (number < 100)
        {
            int t = number / 10;
            int u = number % 10;
            return Tens[t] + (u > 0 ? " " + Units[u] : "");
        }

        if (number < 1000)
        {
            if (number == 100)
                return "ΕΚΑΤΟ";

            int h = number / 100;
            int remainder = number % 100;

            return Hundreds[h] + (remainder > 0 ? " " + NumberToWords(remainder) : "");
        }

        if (number < 1000000)
        {
            int thousands = number / 1000;
            int remainder = number % 1000;

            string thousandsText = thousands == 1
                ? "ΧΙΛΙΑ"
                : NumberToWords(thousands) + " ΧΙΛΙΑΔΕΣ";

            return thousandsText + (remainder > 0 ? " " + NumberToWords(remainder) : "");
        }

        return number.ToString();
    }
}
