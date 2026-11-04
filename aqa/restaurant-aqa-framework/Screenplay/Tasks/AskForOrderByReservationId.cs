using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class AskForOrderByReservationId(string? reservationId = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var apiContext = actor.UsingTheApi();
        var ordersApi = new OrdersApi(apiContext);

        var resId = reservationId;
        if (string.IsNullOrEmpty(resId))
        {
            if (actor.TryRecall<GroupReservationContext>(out var context))
                resId = context.Reservation.Id;
            else
                throw new InvalidOperationException("No reservation found in actor memory.");
        }

        var waiterSession = await actor.AsksForAsync(new TheWaiterSession());
        var response = await ordersApi.GetOrderAsync(resId, waiterSession.IdToken);
        await actor.TheLastResponse().CaptureAsync(response);
    }
}

