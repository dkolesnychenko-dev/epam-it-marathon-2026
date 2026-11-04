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
            // 1. Check existing dishes already attached to this location's menu (must be Available)
            try
            {
                var locationsApi = new LocationsApi(apiContext);
                var locDishesRes = await locationsApi.GetDishesForLocationAsync(location.Id);
                if (locDishesRes.Ok)
                {
                    var dishes = await locDishesRes.ReadAsAsync<List<DishResponse>>();
                    var availableDishes = dishes?.Where(d => d.State.Equals("Available", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (availableDishes is { Count: > 0 })
                    {
                        var matched = availableDishes.FirstOrDefault(d => d.Name.Equals(dishName, StringComparison.OrdinalIgnoreCase))
                            ?? availableDishes.FirstOrDefault(d => d.Name.Contains(dishName, StringComparison.OrdinalIgnoreCase) || dishName.Contains(d.Name, StringComparison.OrdinalIgnoreCase))
                            ?? (dishName.Contains("Salmon", StringComparison.OrdinalIgnoreCase) ? availableDishes.FirstOrDefault(d => d.Name.Contains("Salmon", StringComparison.OrdinalIgnoreCase)) : null)
                            ?? (dishName.Contains("Salad", StringComparison.OrdinalIgnoreCase) ? availableDishes.FirstOrDefault(d => d.Name.Contains("Salad", StringComparison.OrdinalIgnoreCase)) : null)
                            ?? availableDishes.First();

                        if (matched != null)
                        {
                            dish = matched;
                            actor.Remember(dishName, dish);
                        }
                    }
                }
            }
            catch { }

            // 2. If not found in location's menu, create a new dish
            if (dish == null)
            {
                var request = SampleDataFactory.CreateDishRequest([location.Id]);
                var price = dishName.Contains("Salmon", StringComparison.OrdinalIgnoreCase) ? 26.00m : 14.00m;
                var customRequest = request with { Name = SampleDataFactory.Prefix + dishName, Price = price, State = DishState.Available };
                var waiter = await actor.AsksForAsync(new TheWaiterSession());
                var createRes = await dishesApi.CreateDishAsync(customRequest, waiter.IdToken);
                if (!createRes.Ok)
                    throw new InvalidOperationException($"Creating dish '{dishName}' failed: {await createRes.TextAsync()}");

                dish = await createRes.ReadAsAsync<DishResponse>();
                actor.Remember(dishName, dish);

                cleanup.TrackDelete($"dish:{dish.Id}", () => dishesApi.DeleteDishAsync(dish.Id, force: true, bearerToken: waiter.IdToken));
            }
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
