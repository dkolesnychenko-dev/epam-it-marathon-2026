using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed class TheGrandTotalShown : IQuestion<decimal>
{
    public async Task<decimal> AnsweredByAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);
        return await groupOrderPage.GetGrandTotalAsync();
    }
}

