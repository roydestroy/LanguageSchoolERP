using CommunityToolkit.Mvvm.ComponentModel;

namespace LanguageSchoolERP.App.ViewModels;

public partial class ProgramEditViewModel : ObservableObject
{
    [ObservableProperty] private int id;
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private bool hasTransport;
    [ObservableProperty] private bool hasStudyLab;
    [ObservableProperty] private bool hasBooks;

    public string DialogTitle => Id == 0 ? "Νέο πρόγραμμα" : "Επεξεργασία προγράμματος";

    public bool TryValidate(out string validationMessage)
    {
        Name = (Name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(Name))
        {
            validationMessage = "Το όνομα προγράμματος είναι υποχρεωτικό.";
            return false;
        }

        if (Name.Length > 200)
        {
            validationMessage = "Το όνομα προγράμματος δεν μπορεί να υπερβαίνει τους 200 χαρακτήρες.";
            return false;
        }

        validationMessage = string.Empty;
        return true;
    }
}
