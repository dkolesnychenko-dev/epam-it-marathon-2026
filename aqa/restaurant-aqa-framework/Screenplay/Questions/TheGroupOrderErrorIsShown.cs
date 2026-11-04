using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed record GroupOrderErrorStatus(bool IsShown, string ErrorType, string Message);

public sealed class TheGroupOrderErrorIsShown : IQuestion<GroupOrderErrorStatus>
{
    public async Task<GroupOrderErrorStatus> AnsweredByAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);

        try
        {
            await groupOrderPage.NotFoundError.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 6000 });
            var msg = (await groupOrderPage.NotFoundError.TextContentAsync())?.Trim() ?? string.Empty;
            return new GroupOrderErrorStatus(true, "NotFound", msg);
        }
        catch
        {
            try
            {
                await groupOrderPage.GeneralError.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 2000 });
                var msg = (await groupOrderPage.GeneralError.TextContentAsync())?.Trim() ?? string.Empty;
                return new GroupOrderErrorStatus(true, "General", msg);
            }
            catch
            {
                return new GroupOrderErrorStatus(false, string.Empty, string.Empty);
            }
        }
    }
}
