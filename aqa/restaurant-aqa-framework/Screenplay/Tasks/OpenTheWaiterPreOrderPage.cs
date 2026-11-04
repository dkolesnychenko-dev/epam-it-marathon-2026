using System.Text.Json;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class OpenTheWaiterPreOrderPage(string? reservationId = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var resId = reservationId;
        if (string.IsNullOrEmpty(resId) && actor.TryRecall<GroupReservationContext>(out var context))
        {
            resId = context.Reservation.Id;
        }

        resId ??= "00000000-0000-0000-0000-000000000000";

        var waiterSession = await actor.AsksForAsync(new TheWaiterSession());
        var host = new Uri(Settings.Current.UiBaseUrl).Host;
        await page.Context.AddCookiesAsync([
            new()
            {
                Name = "token",
                Value = waiterSession.IdToken,
                Domain = host,
                Path = "/"
            }
        ]);

        await page.GotoAsync(Settings.Current.UiBaseUrl);
        await page.EvaluateAsync("""
            ([authJson, waiterNavJson]) => {
                localStorage.setItem('auth', authJson);
                sessionStorage.setItem('waiterPreOrderData', waiterNavJson);
            }
        """, new[]
        {
            JsonSerializer.Serialize(new
            {
                idToken = waiterSession.IdToken,
                accessToken = waiterSession.IdToken
            }),
            JsonSerializer.Serialize(new
            {
                reservationId = resId,
                createdByName = "Owner",
                createdByRole = "Customer",
                tableName = "Table"
            })
        });
        await page.GotoAsync($"{Settings.Current.UiBaseUrl.TrimEnd('/')}/waiter-pre-order");
    }
}

