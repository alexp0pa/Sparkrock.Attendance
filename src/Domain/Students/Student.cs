namespace Domain.Students;

public sealed class Student
{
    public int Id { get; set; }

    // The school the student is enrolled at (docs/adr/0011-student-school-and-seed-command.md);
    // attendance records still carry their own SchoolId (ADR 0008).
    public int SchoolId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}
