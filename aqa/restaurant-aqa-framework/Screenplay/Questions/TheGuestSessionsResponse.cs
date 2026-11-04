using Restaurant_Aqa_Framework.Api.Dtos.GuestSessions;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed class TheGuestSessionsResponse : IQuestion<GuestSessionsResponse>
{
    public Task<GuestSessionsResponse> AnsweredByAsync(Actor actor) =>
        Task.FromResult(actor.TheLastResponse().Json<GuestSessionsResponse>());
}

