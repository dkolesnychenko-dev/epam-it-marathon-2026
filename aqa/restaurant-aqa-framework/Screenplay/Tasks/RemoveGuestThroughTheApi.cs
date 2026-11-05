using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class RemoveGuestThroughTheApi(
    string? guestName = null,
    string? customGuestSessionId = null,
    string? customGroupReservationId = null,
    string? asRole = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var apiContext = actor.UsingTheApi();
        var guestSessionsApi = new GuestSessionsApi(apiContext);
        var authApi = new AuthApi(apiContext);

        string? groupId = customGroupReservationId;
        string? sessionId = customGuestSessionId;

        actor.TryRecall<GroupReservationContext>(out var context);
        if (context is not null)
        {
            groupId ??= context.GroupReservationId;
            if (sessionId == null && !string.IsNullOrEmpty(guestName))
            {
                var match = context.Guests.FirstOrDefault(g => g.GuestName.Equals(guestName, StringComparison.OrdinalIgnoreCase));
                sessionId = match?.SessionId;
            }
        }

        groupId ??= "00000000-0000-0000-0000-000000000000";
        sessionId ??= "00000000-0000-0000-0000-000000000000";

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
            bearerToken = context?.OwnerBearerToken;
        }

        var response = await guestSessionsApi.DeleteGuestSessionAsync(groupId, sessionId, bearerToken);
        await actor.TheLastResponse().CaptureAsync(response);

        if (response.Ok && context is not null && !string.IsNullOrEmpty(guestName))
        {
            var guest = context.Guests.FirstOrDefault(g => g.GuestName.Equals(guestName, StringComparison.OrdinalIgnoreCase));
            if (guest != null)
            {
                context.Guests.Remove(guest);
            }
        }
    }
}

