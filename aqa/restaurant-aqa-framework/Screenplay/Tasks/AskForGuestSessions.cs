using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class AskForGuestSessions(string? groupReservationId = null, string? asRole = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var apiContext = actor.UsingTheApi();
        var guestSessionsApi = new GuestSessionsApi(apiContext);
        var authApi = new AuthApi(apiContext);

        var groupId = groupReservationId;
        if (string.IsNullOrEmpty(groupId))
        {
            if (actor.TryRecall<GroupReservationContext>(out var context))
                groupId = context.GroupReservationId;
            else
                throw new InvalidOperationException("No group reservation found in actor memory.");
        }

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
            if (actor.TryRecall<GroupReservationContext>(out var context))
            {
                bearerToken = context.OwnerBearerToken;
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

        var response = await guestSessionsApi.GetGuestSessionsAsync(groupId, bearerToken);
        await actor.TheLastResponse().CaptureAsync(response);
    }
}
