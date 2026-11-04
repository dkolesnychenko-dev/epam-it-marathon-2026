using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Screenplay.Abilities;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class EnsureOrderExists : ITask
{
    private const string KeyOrderProvisioned = "OrderProvisionedForGroup";
    private const string DefaultDishId = "b1111111-1111-1111-1111-111111111111"; // Grilled Wild Salmon with Asparagus

    public async Task PerformAsAsync(Actor actor)
    {
        if (actor.TryRecall<bool>(KeyOrderProvisioned, out var alreadyProvisioned) && alreadyProvisioned)
            return;

        if (!actor.TryRecall<GroupReservationContext>(out var context))
            return;

        var apiContext = actor.UsingTheApi();
        var ordersApi = new OrdersApi(apiContext);

        try
        {
            var createOrderReq = new CreateOrderRequest(
                context.Reservation.Id,
                [new OrderItemRequest(DefaultDishId, 1, OrderItemState.Pending)]);

            var response = await ordersApi.CreateOrderAsync(createOrderReq, context.OwnerBearerToken);
            if (response.Ok || response.Status == 400)
            {
                actor.Remember(KeyOrderProvisioned, true);
            }
        }
        catch
        {
            // Best effort provisioning
        }
    }
}

