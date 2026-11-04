using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.Locations;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class ModifyGuestCart(string guestName, string dishName, int quantity = 1, bool add = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var groupContext = actor.Recall<GroupReservationContext>();
        var guest = groupContext.Guests.FirstOrDefault(g => g.GuestName.Equals(guestName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Guest '{guestName}' not found in current group reservation context.");

        var location = actor.Recall<LocationResponse>();
        var cleanup = actor.Recall<CleanupScope>();
        var apiContext = actor.UsingTheApi();
        var dishesApi = new DishesApi(apiContext);
        var guestSessionsApi = new GuestSessionsApi(apiContext);

        if (!actor.TryRecall<DishResponse>(dishName, out var dish))
        {
            var request = SampleDataFactory.CreateDishRequest([location.Id]);
            var price = dishName.Contains("Salmon", StringComparison.OrdinalIgnoreCase) ? 25.00m : 15.00m;
            var customRequest = request with { Name = SampleDataFactory.Prefix + dishName, Price = price };
            var createRes = await dishesApi.CreateDishAsync(customRequest);
            if (!createRes.Ok)
                throw new InvalidOperationException($"Creating dish '{dishName}' failed: {await createRes.TextAsync()}");

            dish = await createRes.ReadAsAsync<DishResponse>();
            actor.Remember(dishName, dish);

            var waiter = await actor.AsksForAsync(new TheWaiterSession());
            cleanup.TrackDelete($"dish:{dish.Id}", () => dishesApi.DeleteDishAsync(dish.Id, force: true, bearerToken: waiter.IdToken));
        }

        if (add)
        {
            var addRes = await guestSessionsApi.AddDishAsync(guest.SessionId, dish.Id, quantity, guest.SessionToken);
            if (!addRes.Ok)
                throw new InvalidOperationException($"Adding dish '{dishName}' to guest '{guestName}' failed: {await addRes.TextAsync()}");

            guest.Dishes.Add((dish, quantity));
        }
        else
        {
            var removeRes = await guestSessionsApi.RemoveDishAsync(guest.SessionId, dish.Id, quantity, guest.SessionToken);
            if (!removeRes.Ok)
                throw new InvalidOperationException($"Removing dish '{dishName}' from guest '{guestName}' failed: {await removeRes.TextAsync()}");

            var index = guest.Dishes.FindIndex(d => d.Dish.Id == dish.Id);
            if (index >= 0)
                guest.Dishes.RemoveAt(index);
        }
    }
}
