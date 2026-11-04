using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class SubmitTheConsolidatedPreOrderInUi : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);

        await actor.AttemptsToAsync(new EnsureOrderExists());

        await Assertions.Expect(groupOrderPage.SubmitButton).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(groupOrderPage.SubmitButton).ToBeEnabledAsync(new() { Timeout = 5000 });

        await groupOrderPage.OpenSubmitModalAsync();

        await Assertions.Expect(groupOrderPage.ConfirmSubmitButton).ToBeVisibleAsync(new() { Timeout = 5000 });

        var responseTask = page.WaitForResponseAsync(
            r => r.Url.Contains("/group-reservations/", StringComparison.OrdinalIgnoreCase) && r.Url.EndsWith("/submit", StringComparison.OrdinalIgnoreCase),
            new() { Timeout = 10000 });

        await groupOrderPage.ConfirmSubmitAsync();

        try
        {
            await responseTask;
        }
        catch
        {
            await Task.Delay(1000);
        }
    }
}
