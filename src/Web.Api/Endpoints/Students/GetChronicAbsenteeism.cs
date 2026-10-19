using Application.Abstractions.Messaging;
using Application.Students.GetChronicAbsenteeism;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Students;

internal sealed class GetChronicAbsenteeism : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("students/{studentId:int}/chronic-absenteeism", async (
            int studentId,
            string? schoolYear,
            IQueryHandler<GetStudentChronicAbsenteeismQuery, StudentChronicAbsenteeismResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetStudentChronicAbsenteeismQuery(studentId, schoolYear);

            Result<StudentChronicAbsenteeismResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Students)
        .RequireAuthorization();
    }
}
