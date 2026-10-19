using Application.Abstractions.Messaging;
using Application.Attendance.SaveDaily;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Attendance;

internal sealed class SaveDaily : IEndpoint
{
    public sealed record Record(int StudentId, string Status, int? MinutesLate, string? Notes);

    public sealed record Request(int SchoolId, List<Record?>? Records);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("attendance/{date:datetime}", async (
            DateOnly date,
            Request request,
            ICommandHandler<SaveDailyAttendanceCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new SaveDailyAttendanceCommand(
                date,
                request.SchoolId,
                // A null entry becomes an empty one, so the validator rejects it with 400 instead of a 500.
                [.. (request.Records ?? []).Select(r =>
                    new StudentAttendanceEntry(r?.StudentId ?? 0, r?.Status ?? string.Empty, r?.MinutesLate, r?.Notes))]);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Attendance)
        .RequireAuthorization();
    }
}
