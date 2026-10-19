using Application.Abstractions.Messaging;

namespace Application.Students.GetChronicAbsenteeism;

// SchoolYear is 'YYYY-YYYY'; null means the current school year.
public sealed record GetStudentChronicAbsenteeismQuery(int StudentId, string? SchoolYear)
    : IQuery<StudentChronicAbsenteeismResponse>;
