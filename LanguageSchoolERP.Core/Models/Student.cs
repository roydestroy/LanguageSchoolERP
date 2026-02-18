namespace LanguageSchoolERP.Core.Models;

public class Student
{
    public Guid StudentId { get; set; } = Guid.NewGuid();

    public string FullName { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }

    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";

    public string FatherName { get; set; } = "";
    public string FatherContact { get; set; } = "";

    public string MotherName { get; set; } = "";
    public string MotherContact { get; set; } = "";

    public PreferredPhoneSource PreferredPhoneSource { get; set; } = PreferredPhoneSource.Student;
    public PreferredEmailSource PreferredEmailSource { get; set; } = PreferredEmailSource.Student;

    public string Notes { get; set; } = "";

    public bool Discontinued { get; set; }
    public bool NonCollectable { get; set; }

    public ICollection<Enrollment> Enrollments { get; set; }
        = new List<Enrollment>();

    public ICollection<Contract> Contracts { get; set; }
        = new List<Contract>();
}

public enum PreferredPhoneSource
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
