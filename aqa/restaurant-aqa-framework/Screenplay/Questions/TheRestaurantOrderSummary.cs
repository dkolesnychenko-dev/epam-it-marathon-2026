using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed class TheRestaurantOrderSummary : IQuestion<IReadOnlyList<(string DishName, int Quantity, string TotalPrice)>>
{
    public async Task<IReadOnlyList<(string DishName, int Quantity, string TotalPrice)>> AnsweredByAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);

        await Assertions.Expect(groupOrderPage.ConsolidatedSummary).ToBeVisibleAsync(new() { Timeout = 10000 });
        return await groupOrderPage.GetConsolidatedItemsAsync();
    }
}

