using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed class TheWaiterOrderResponse : IQuestion<GetOrderResponse>
{
    public async Task<GetOrderResponse> AnsweredByAsync(Actor actor)
    {
        var response = actor.TheLastResponse().Response;
        if (response == null)
            throw new InvalidOperationException("No HTTP response captured in actor memory.");

        return await response.ReadAsAsync<GetOrderResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize order response.");
    }
}

