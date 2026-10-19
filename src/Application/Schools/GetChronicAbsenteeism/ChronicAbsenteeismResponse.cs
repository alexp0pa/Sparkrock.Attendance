namespace Application.Schools.GetChronicAbsenteeism;

public sealed record ChronicAbsenteeismResponse
{
    public int SchoolId { get; init; }
    public string SchoolYear { get; init; }
    public int Threshold { get; init; }
    public List<ChronicallyAbsentStudentResponse> Students { get; init; } = [];
}

public sealed record ChronicallyAbsentStudentResponse
{
    public int StudentId { get; init; }
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public int ExcusedAbsences { get; init; }
    public int UnexcusedAbsences { get; init; }
    public int TotalAbsences { get; init; }
}
