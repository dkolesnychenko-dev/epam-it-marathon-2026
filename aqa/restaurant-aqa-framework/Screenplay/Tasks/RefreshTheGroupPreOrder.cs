using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class RefreshTheGroupPreOrder : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);

        var responseTask = page.WaitForResponseAsync(
            r => r.Url.Contains("/guest/groups/") && r.Status == 200,
            new() { Timeout = 7000 });

        await groupOrderPage.RefreshAsync();

        try
        {
            await responseTask;
        }
        catch
        {
            // Fallback: brief delay for client rendering if response already arrived
            await Task.Delay(500);
        }
    }
}
