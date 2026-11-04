using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class OpenTheGroupPreOrderPage(string? reservationId = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var resId = reservationId;
        if (string.IsNullOrEmpty(resId))
        {
            if (actor.TryRecall<GroupReservationContext>(out var groupContext))
                resId = groupContext.Reservation.Id;
            else if (actor.TryRecall<ReservationResponse>(out var res))
                resId = res.Id;
            else
                throw new InvalidOperationException("No reservation found in actor memory to open the group pre-order page.");
        }

        // If not already signed in, ensure the browser session has an authenticated customer so AuthGuard allows navigation
        if (!actor.TryRecall<GroupReservationContext>(out _))
        {
            var authApi = new AuthApi(actor.UsingTheApi());
            var user = SampleDataFactory.RegisterRequest();
            var regRes = await authApi.RegisterAsync(user);
            if (regRes.Ok)
            {
                await actor.AttemptsToAsync(new SignInThroughTheLoginForm(user.Email, user.Password));
            }
        }

        var page = actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);
        await groupOrderPage.NavigateAsync(resId);
    }
}

