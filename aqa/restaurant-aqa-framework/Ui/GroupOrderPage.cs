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
}

