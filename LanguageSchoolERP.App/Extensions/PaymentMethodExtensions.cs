using LanguageSchoolERP.Core.Models;

namespace LanguageSchoolERP.App.Extensions;

public static class PaymentMethodExtensions
{
    public static string ToGreekLabel(this PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Μετρητά",
        PaymentMethod.Card => "Κάρτα",
        PaymentMethod.BankTransfer => "Τραπεζική μεταφορά",
        PaymentMethod.IRIS => "IRIS",
        PaymentMethod.Other => "Άλλο",
        _ => method.ToString()
    };
}
