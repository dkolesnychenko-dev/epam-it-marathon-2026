using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Ui;

public sealed class GroupOrderPage(IPage page) : BasePage(page)
{
    public HeaderComponent Header { get; } = new(page);

    public ILocator Title => Page.Locator("h1.title");
    public ILocator StatusBadge => Page.Locator(".header .status-badge");
    public ILocator UnsubmittedBanner => Page.Locator(".unsubmitted-banner");
    public ILocator EmptyDishesBanner => Page.Locator(".empty-dishes-banner");
    public ILocator NotFoundError => Page.Locator(".error-container.not-found .error-text");
    public ILocator GeneralError => Page.Locator(".error-container.general-error .error-text");
    public ILocator GrandTotal => Page.Locator("footer.footer-summary .grand-total-amount");
    public ILocator RefreshButton => Page.Locator("button.refresh-btn");
    public ILocator SubmitButton => Page.Locator("button.submit-button");
    public ILocator SubmitModal => Page.Locator(".modal-card[role='dialog'], dialog, .modal-dialog").Filter(new() { HasText = "Submit Pre-Order" });
    public ILocator ConfirmSubmitButton => Page.Locator("button.confirm-btn");
    public ILocator CancelSubmitButton => Page.Locator("button.cancel-btn");
    public ILocator RemoveGuestModal => Page.Locator(".modal-card[role='dialog'], dialog, .modal-dialog").Filter(new() { HasText = "Remove Guest" });
    public ILocator ConfirmRemoveGuestButton => Page.Locator("button.remove-btn");
    public ILocator CancelRemoveGuestButton => Page.Locator("button.cancel-btn");
    public ILocator ConsolidatedSummary => Page.Locator("section.consolidated-section");
    public ILocator ConsolidatedItems => Page.Locator("section.consolidated-section ul.consolidated-list > li.consolidated-item");

    public ILocator Participants => Page.Locator(".participants-section ul.participants-list > li.participant-item");

    public Task NavigateAsync(string reservationId) =>
        NavigateToAsync($"/reservations/{reservationId}/group-order");

    public ParticipantCardComponent Participant(int index) => new(Participants.Nth(index));

    public ParticipantCardComponent ParticipantByName(string name) =>
        new(Participants.Filter(new() { HasText = name }));

    public Task<int> ParticipantsCountAsync() => Participants.CountAsync();

    public async Task<string> GetGrandTotalTextAsync() =>
        (await GrandTotal.TextContentAsync())?.Trim() ?? string.Empty;

    public async Task<decimal> GetGrandTotalAsync()
    {
        var text = await GetGrandTotalTextAsync();
        var match = System.Text.RegularExpressions.Regex.Match(text, @"[\d.,]+");
        if (match.Success && decimal.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value))
            return value;

        return 0m;
    }

    public Task RefreshAsync() => RefreshButton.ClickAsync();

    public async Task OpenSubmitModalAsync() => await SubmitButton.ClickAsync();
    public async Task ConfirmSubmitAsync() => await ConfirmSubmitButton.ClickAsync();
    public async Task CancelSubmitAsync() => await CancelSubmitButton.ClickAsync();

    public async Task ConfirmRemoveGuestAsync() => await ConfirmRemoveGuestButton.ClickAsync();
    public async Task CancelRemoveGuestAsync() => await CancelRemoveGuestButton.ClickAsync();

    public async Task<IReadOnlyList<(string DishName, int Quantity, string TotalPrice)>> GetConsolidatedItemsAsync()
    {
        var count = await ConsolidatedItems.CountAsync();
        var result = new List<(string DishName, int Quantity, string TotalPrice)>(count);
        for (var i = 0; i < count; i++)
        {
            var item = ConsolidatedItems.Nth(i);
            var nameQty = (await item.Locator(".dish-name-qty").TextContentAsync())?.Trim() ?? string.Empty;
            var total = (await item.Locator(".dish-total").TextContentAsync())?.Trim() ?? string.Empty;

            var parts = nameQty.Split('×', StringSplitOptions.TrimEntries);
            var dishName = parts.Length > 0 ? parts[0] : nameQty;
            var qty = 1;
            if (parts.Length > 1 && int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var parsedQty))
            {
                qty = parsedQty;
            }

            result.Add((dishName, qty, total));
        }

        return result;
    }
}
