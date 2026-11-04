using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupReservations;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed class TheConsolidatedDishesResponse : IQuestion<IReadOnlyList<ConsolidatedDishDto>>
{
    public async Task<IReadOnlyList<ConsolidatedDishDto>> AnsweredByAsync(Actor actor)
    {
        var response = actor.TheLastResponse().Response;
        if (response == null)
            throw new InvalidOperationException("No HTTP response captured in actor memory.");

        return await response.ReadAsAsync<List<ConsolidatedDishDto>>() ?? [];
    }
}

