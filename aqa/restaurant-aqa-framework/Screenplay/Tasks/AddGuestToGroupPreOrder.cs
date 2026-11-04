using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.GuestSessions;
using Restaurant_Aqa_Framework.Api.Dtos.Locations;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class AddGuestToGroupPreOrder(
    string guestName,
    IReadOnlyList<(string DishName, int Quantity)> items,
    bool isSubmitted = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var groupContext = actor.Recall<GroupReservationContext>();
        var location = actor.Recall<LocationResponse>();
        var cleanup = actor.Recall<CleanupScope>();
        var apiContext = actor.UsingTheApi();
        var dishesApi = new DishesApi(apiContext);
        var guestSessionsApi = new GuestSessionsApi(apiContext);

        // 1. Create guest session
        var createSessionRes = await guestSessionsApi.CreateGuestSessionAsync(
            new CreateGuestSessionRequest(groupContext.GroupReservationId, guestName));

        if (!createSessionRes.Ok)
            throw new InvalidOperationException($"Creating guest session failed ({createSessionRes.Status}): {await createSessionRes.TextAsync()}");

        var session = await createSessionRes.ReadAsAsync<CreateGuestSessionResponse>();

        var guest = new GuestContext
        {
            GuestName = guestName,
            SessionId = session.SessionId,
            SessionToken = session.SessionToken,
            IsSubmitted = isSubmitted
        };

        // 2. Add dishes
        foreach (var (dishName, quantity) in items)
        {
            if (string.IsNullOrWhiteSpace(dishName) || dishName.Equals("(none)", StringComparison.OrdinalIgnoreCase))
                continue;

            // Resolve from location's existing menu or create dish
            var dish = await GetOrCreateDishAsync(actor, apiContext, dishesApi, location.Id, cleanup, dishName);

            var addRes = await guestSessionsApi.AddDishAsync(session.SessionId, dish.Id, quantity, session.SessionToken);
            if (!addRes.Ok)
                throw new InvalidOperationException($"Adding dish '{dishName}' failed ({addRes.Status}): {await addRes.TextAsync()}");

            guest.Dishes.Add((dish, quantity));
        }

        // 3. Submit cart if requested
        if (isSubmitted && guest.Dishes.Count > 0)
        {
            var submitRes = await guestSessionsApi.SubmitCartAsync(session.SessionId, session.SessionToken);
            if (!submitRes.Ok)
                throw new InvalidOperationException($"Submitting cart for guest '{guestName}' failed ({submitRes.Status}): {await submitRes.TextAsync()}");
        }

        groupContext.Guests.Add(guest);
    }

    private static async Task<DishResponse> GetOrCreateDishAsync(
        Actor actor,
        IAPIRequestContext apiContext,
        DishesApi dishesApi,
        string locationId,
        CleanupScope cleanup,
        string dishName)
    {
        if (actor.TryRecall<DishResponse>(dishName, out var existingDish))
            return existingDish;

        // 1. Check existing dishes already attached to this location's menu (must be Available)
        try
        {
            var locationsApi = new LocationsApi(apiContext);
            var locDishesRes = await locationsApi.GetDishesForLocationAsync(locationId);
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
                        actor.Remember(dishName, matched);
                        return matched;
                    }
                }
            }
        }
        catch { }

        // 2. If not found in location's menu, create a new dish associated with this location
        var request = SampleDataFactory.CreateDishRequest([locationId]);
        var price = dishName.Contains("Salmon", StringComparison.OrdinalIgnoreCase) ? 26.00m : 14.00m;
        var customRequest = request with { Name = SampleDataFactory.Prefix + dishName, Price = price, State = DishState.Available };

        var waiter = await actor.AsksForAsync(new TheWaiterSession());
        var createRes = await dishesApi.CreateDishAsync(customRequest, waiter.IdToken);
        if (!createRes.Ok)
            throw new InvalidOperationException($"Creating dish '{dishName}' failed ({createRes.Status}): {await createRes.TextAsync()}");

        var createdDish = await createRes.ReadAsAsync<DishResponse>();
        actor.Remember(dishName, createdDish);

        cleanup.TrackDelete($"dish:{createdDish.Id}", () => dishesApi.DeleteDishAsync(createdDish.Id, force: true, bearerToken: waiter.IdToken));
        return createdDish;
    }
}
