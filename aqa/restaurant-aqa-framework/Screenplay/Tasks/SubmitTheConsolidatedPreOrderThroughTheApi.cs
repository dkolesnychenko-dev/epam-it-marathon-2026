using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class SubmitTheConsolidatedPreOrderThroughTheApi(
    string? asRole = null,
    string? customGroupReservationId = null,
    string? customReservationId = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var apiContext = actor.UsingTheApi();
        var groupReservationsApi = new GroupReservationsApi(apiContext);
        var authApi = new AuthApi(apiContext);

        string? groupId = customGroupReservationId;
        string? reservationId = customReservationId;

        actor.TryRecall<GroupReservationContext>(out var context);
        if (context is not null)
        {
            groupId ??= context.GroupReservationId;
            reservationId ??= context.Reservation.Id;
        }

        groupId ??= "00000000-0000-0000-0000-000000000000";
        reservationId ??= "00000000-0000-0000-0000-000000000000";

        string? bearerToken = null;
        if (asRole == "unauthorized")
        {
            bearerToken = null;
        }
        else if (asRole == "waiter")
        {
            var waiter = await actor.AsksForAsync(new TheWaiterSession());
            bearerToken = waiter.IdToken;
        }
        else if (asRole == "another_customer")
        {
            var otherUser = SampleDataFactory.RegisterRequest();
            var regRes = await authApi.RegisterAsync(otherUser);
            if (!regRes.Ok)
                throw new InvalidOperationException($"Registering second customer failed: {await regRes.TextAsync()}");

            var signInRes = await authApi.SignInAsync(new SignInRequest(otherUser.Email, otherUser.Password));
            if (!signInRes.Ok)
                throw new InvalidOperationException($"Signing in second customer failed: {await signInRes.TextAsync()}");

            var tokens = await signInRes.ReadAsAsync<SignInResponse>();
            bearerToken = tokens.IdToken;
        }
        else
        {
            if (context is not null)
            {
                bearerToken = context.OwnerBearerToken;
                var hasSubmittedGuestDishes = context.Guests.Any(g => g.IsSubmitted && g.Dishes.Any(d => d.Quantity > 0));
                if (customGroupReservationId == null && customReservationId == null && hasSubmittedGuestDishes)
                {
                    await actor.AttemptsToAsync(new EnsureOrderExists());
                }
            }
            else
            {
                var user = SampleDataFactory.RegisterRequest();
                await authApi.RegisterAsync(user);
                var signInRes = await authApi.SignInAsync(new SignInRequest(user.Email, user.Password));
                if (signInRes.Ok)
                {
                    var tokens = await signInRes.ReadAsAsync<SignInResponse>();
                    bearerToken = tokens.IdToken;
                }
            }
        }

        var response = await groupReservationsApi.SubmitConsolidatedPreOrderAsync(groupId, reservationId, bearerToken);
        await actor.TheLastResponse().CaptureAsync(response);
    }
}

