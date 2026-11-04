using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui;

public sealed class WaiterPreOrderPage(IPage page) : BasePage(page)
{
    public ILocator Title => Page.Locator("h2.title");
    public ILocator DishCards => Page.Locator(".preorder-list app-dish-card");

    public async Task<IReadOnlyList<(string DishName, int Quantity)>> GetPreOrderItemsAsync()
    {
        var count = await DishCards.CountAsync();
        var result = new List<(string DishName, int Quantity)>(count);
        for (var i = 0; i < count; i++)
        {
            var card = DishCards.Nth(i);
            var name = (await card.Locator(".dish-name, h4.dish-name, .dish-title, h3, h4").First.TextContentAsync())?.Trim() ?? string.Empty;
            var qtyText = (await card.Locator(".pre-order-quantity").TextContentAsync())?.Trim() ?? string.Empty;

            var match = System.Text.RegularExpressions.Regex.Match(qtyText, @"\d+");
            var qty = match.Success && int.TryParse(match.Value, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 1;

            result.Add((name, qty));
        }

        return result;
    }
}

