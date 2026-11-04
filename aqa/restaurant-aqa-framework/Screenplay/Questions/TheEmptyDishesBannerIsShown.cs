using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed class TheEmptyDishesBannerIsShown : IQuestion<bool>
{
    public async Task<bool> AnsweredByAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);
        return await groupOrderPage.EmptyDishesBanner.IsVisibleAsync();
    }
}

