using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed class TheEmptyDishesBannerIsShown : IQuestion<bool>
{
    public async Task<bool> AnsweredByAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);
        try
        {
            await groupOrderPage.EmptyDishesBanner.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 8000 });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
