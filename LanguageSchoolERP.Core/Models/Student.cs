using System.ComponentModel.DataAnnotations.Schema;

namespace LanguageSchoolERP.Core.Models;

public class Student
{
    public Guid StudentId { get; set; } = Guid.NewGuid();

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }

    public string Mobile { get; set; } = "";
    public string Landline { get; set; } = "";
    public string Email { get; set; } = "";

    public string FatherName { get; set; } = "";
    public string FatherEmail { get; set; } = "";
    public string FatherMobile { get; set; } = "";
    public string FatherLandline { get; set; } = "";

    public string MotherName { get; set; } = "";
    public string MotherEmail { get; set; } = "";
    public string MotherMobile { get; set; } = "";
    public string MotherLandline { get; set; } = "";

    public PreferredPhoneSource PreferredPhoneSource { get; set; } = PreferredPhoneSource.Student;
    public PreferredLandlineSource PreferredLandlineSource { get; set; } = PreferredLandlineSource.Student;
    public PreferredEmailSource PreferredEmailSource { get; set; } = PreferredEmailSource.Student;

    public string Notes { get; set; } = "";

    public bool Discontinued { get; set; }
    public bool NonCollectable { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; }
        = new List<Enrollment>();

    public ICollection<Contract> Contracts { get; set; }
        = new List<Contract>();

    [NotMapped]
    public string FullName
    {
        get => string.Join(" ", new[] { FirstName?.Trim(), LastName?.Trim() }.Where(x => !string.IsNullOrWhiteSpace(x)));
        set
        {
            var parts = (value ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 0)
            {
                FirstName = "";
                LastName = "";
            }
            else if (parts.Length == 1)
            {
                FirstName = parts[0];
                LastName = "";
            }
            else
            {
                FirstName = string.Join(' ', parts.Take(parts.Length - 1));
                LastName = parts[^1];
            }
        }
    }

    [NotMapped]
    public string Phone
    {
        get => Mobile;
        set => Mobile = value ?? string.Empty;
    }
}

public enum PreferredPhoneSource
{
    Student = 0,
    Father = 1,
    Mother = 2
}

public enum PreferredLandlineSource
{
    Student = 0,
    Father = 1,
    Mother = 2
}

public enum PreferredEmailSource
{
    Student = 0,
    Father = 1,
    Mother = 2
}
