using Application.Abstractions.Messaging;
using Application.Students.GetAttendanceHistory;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Students;

internal sealed class GetAttendanceHistory : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("students/{studentId:int}/attendance", async (
            int studentId,
            string? schoolYear,
            IQueryHandler<GetStudentAttendanceHistoryQuery, StudentAttendanceHistoryResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetStudentAttendanceHistoryQuery(studentId, schoolYear);

            Result<StudentAttendanceHistoryResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Students)
        .RequireAuthorization();
    }
}
