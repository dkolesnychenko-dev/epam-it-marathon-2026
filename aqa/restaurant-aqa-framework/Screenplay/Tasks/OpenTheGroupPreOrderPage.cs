using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
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

        var page = actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);
        await groupOrderPage.NavigateAsync(resId);
    }
}

