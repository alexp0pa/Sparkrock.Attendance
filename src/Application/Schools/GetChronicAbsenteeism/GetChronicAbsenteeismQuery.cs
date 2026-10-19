using Application.Abstractions.Messaging;

namespace Application.Schools.GetChronicAbsenteeism;

// SchoolYear is 'YYYY-YYYY'; null means the current school year.
public sealed record GetChronicAbsenteeismQuery(int SchoolId, string? SchoolYear)
    : IQuery<ChronicAbsenteeismResponse>;
