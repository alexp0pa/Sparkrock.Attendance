namespace Application.Students.GetChronicAbsenteeism;

public sealed record StudentChronicAbsenteeismResponse
{
    public int StudentId { get; init; }
    public int SchoolId { get; init; }
    public string SchoolYear { get; init; }
    public int Threshold { get; init; }
    public int ExcusedAbsences { get; init; }
    public int UnexcusedAbsences { get; init; }
    public int TotalAbsences { get; init; }
    public bool IsChronicallyAbsent { get; init; }
}
