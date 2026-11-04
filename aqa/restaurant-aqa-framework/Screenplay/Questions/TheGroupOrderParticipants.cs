using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed record ParticipantViewModel(
    string Name,
    string Status,
    bool HasEmptyDishes,
    IReadOnlyList<(string Name, int Quantity, string Price)> Items,
    string Subtotal);

public sealed class TheGroupOrderParticipants : IQuestion<IReadOnlyList<ParticipantViewModel>>
{
    public async Task<IReadOnlyList<ParticipantViewModel>> AnsweredByAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);

        await Assertions.Expect(groupOrderPage.Participants.First).ToBeVisibleAsync(new() { Timeout = 10000 });

        var count = await groupOrderPage.ParticipantsCountAsync();
        var result = new List<ParticipantViewModel>(count);

        for (var i = 0; i < count; i++)
        {
            var card = groupOrderPage.Participant(i);
            var title = await card.GetTitleAsync();
            var status = await card.GetStatusAsync();
            var isEmpty = await card.EmptyDishesMessage.IsVisibleAsync();
            var items = await card.GetItemsAsync();
            var subtotal = await card.GetSubtotalAsync();

            result.Add(new ParticipantViewModel(title, status, isEmpty, items, subtotal));
        }

        return result;
    }
}

