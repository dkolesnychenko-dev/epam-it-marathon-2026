using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui.Components;

public sealed class ParticipantCardComponent(ILocator root)
{
    public ILocator Root => root;
    public ILocator Title => root.Locator("h2.title");
    public ILocator SubmittedChip => root.Locator(".chip.submitted");
    public ILocator PendingChip => root.Locator(".chip.pending");
    public ILocator EmptyDishesMessage => root.Locator("p.empty");
    public ILocator Items => root.Locator("ul.items > li.item");
    public ILocator SubtotalAmount => root.Locator(".subtotal-row .subtotal span:last-child");
    public ILocator RemoveGuestButton => root.Locator("button.remove-guest-btn");

    public async Task<string> GetTitleAsync() => (await Title.TextContentAsync())?.Trim() ?? string.Empty;

    public async Task<string> GetStatusAsync()
    {
        if (await SubmittedChip.IsVisibleAsync())
            return (await SubmittedChip.TextContentAsync())?.Trim() ?? string.Empty;

        if (await PendingChip.IsVisibleAsync())
            return (await PendingChip.TextContentAsync())?.Trim() ?? string.Empty;

        return string.Empty;
    }

    public async Task<string> GetSubtotalAsync() =>
        (await SubtotalAmount.TextContentAsync())?.Trim() ?? string.Empty;

    public async Task<IReadOnlyList<(string Name, int Quantity, string Price)>> GetItemsAsync()
    {
        var count = await Items.CountAsync();
        var result = new List<(string Name, int Quantity, string Price)>(count);
        for (var i = 0; i < count; i++)
        {
            var item = Items.Nth(i);
            var name = (await item.Locator(".item-details .name").TextContentAsync())?.Trim() ?? string.Empty;
            var qtyText = (await item.Locator(".item-details .qty").TextContentAsync())?.Trim() ?? string.Empty;
            var price = (await item.Locator("p.price").TextContentAsync())?.Trim() ?? string.Empty;

            var qty = 1;
            var match = System.Text.RegularExpressions.Regex.Match(qtyText, @"\d+");
            if (match.Success && int.TryParse(match.Value, System.Globalization.CultureInfo.InvariantCulture, out var parsedQty))
            {
                qty = parsedQty;
            }

            result.Add((name, qty, price));
        }

        return result;
    }

    public Task RemoveGuestAsync() => RemoveGuestButton.ClickAsync();
}
