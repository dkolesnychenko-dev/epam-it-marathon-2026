using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class RemoveGuestFromGroupInUi(string guestName) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);

        var participantCard = groupOrderPage.ParticipantByName(guestName);
        await Assertions.Expect(participantCard.Root).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(participantCard.RemoveGuestButton).ToBeVisibleAsync(new() { Timeout = 5000 });

        await participantCard.RemoveGuestAsync();

        await Assertions.Expect(groupOrderPage.ConfirmRemoveGuestButton).ToBeVisibleAsync(new() { Timeout = 5000 });

        var responseTask = page.WaitForResponseAsync(
            r => r.Url.Contains("/guest/groups/", StringComparison.OrdinalIgnoreCase) &&
                 r.Url.Contains("/sessions/", StringComparison.OrdinalIgnoreCase) &&
                 r.Request.Method.Equals("DELETE", StringComparison.OrdinalIgnoreCase),
            new() { Timeout = 10000 });

        await groupOrderPage.ConfirmRemoveGuestAsync();

        try
        {
            await responseTask;
        }
        catch
        {
            await Task.Delay(1000);
        }

        await Assertions.Expect(participantCard.Root).ToHaveCountAsync(0);

        if (actor.TryRecall<GroupReservationContext>(out var context))
        {
            var guest = context.Guests.FirstOrDefault(g => g.GuestName.Equals(guestName, StringComparison.OrdinalIgnoreCase));
            if (guest != null)
            {
                context.Guests.Remove(guest);
            }
        }
    }
}

