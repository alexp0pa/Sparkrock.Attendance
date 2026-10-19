using Application.Abstractions.Messaging;
using Application.Schools.GetChronicAbsenteeism;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Schools;

internal sealed class GetChronicAbsenteeism : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("schools/{schoolId:int}/chronic-absenteeism", async (
            int schoolId,
            string? schoolYear,
            IQueryHandler<GetChronicAbsenteeismQuery, ChronicAbsenteeismResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetChronicAbsenteeismQuery(schoolId, schoolYear);

            Result<ChronicAbsenteeismResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Schools)
        .RequireAuthorization();
    }
}
