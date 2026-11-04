using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class GroupPreOrderStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    private static readonly Regex DishItemPattern = new(@"^(.*?)(?:\s*[xX×]\s*(\d+))?$", RegexOptions.Compiled);

    [Given("an owner has booked a table for a group")]
    [Given("an owner has booked a table for a group with no guests joined")]
    [Given("a group pre-order exists")]
    public Task GivenOwnerHasBookedATableForAGroup() =>
        Actor.AttemptsToAsync(new SetUpGroupReservation());

    [Given("the following guests have joined the group pre-order:")]
    public async Task GivenTheFollowingGuestsHaveJoined(Table table)
    {
        foreach (var row in table.Rows)
        {
            var name = row["name"].Trim();
            var dishesText = row["dishes"].Trim();
            var status = row["status"].Trim();
            var isSubmitted = status.Equals("submitted", StringComparison.OrdinalIgnoreCase);

            var items = new List<(string DishName, int Quantity)>();
            if (!dishesText.Equals("(none)", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(dishesText))
            {
                var dishEntries = dishesText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var entry in dishEntries)
                {
                    var match = DishItemPattern.Match(entry);
                    if (match.Success)
                    {
                        var dishName = match.Groups[1].Value.Trim();
                        var quantity = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
                        items.Add((dishName, quantity));
                    }
                }
            }

            await Actor.AttemptsToAsync(new AddGuestToGroupPreOrder(name, items, isSubmitted));
        }
    }

    [When("the owner reviews the group pre-order")]
    [Given("the owner is viewing the group pre-order")]
    public Task WhenOwnerReviewsTheGroupPreOrder() =>
        Actor.AttemptsToAsync(new OpenTheGroupPreOrderPage());

    [When("the owner asks for the group pre-order sessions")]
    public Task WhenOwnerAsksForGroupPreOrderSessions() =>
        Actor.AttemptsToAsync(new AskForGuestSessions());

    [When("the guest modifies their cart by adding another dish")]
    [When("the guest adds another dish to their cart")]
    public Task WhenGuestAddsDishToCart() =>
        Actor.AttemptsToAsync(new ModifyGuestCart("Bob", "Caesar Salad", 1, add: true));

    [When("the guest confirms their cart")]
    [When("the guest submits their cart")]
    public async Task WhenGuestConfirmsTheirCart()
    {
        var context = Recall<GroupReservationContext>();
        var guest = context.Guests.LastOrDefault()
            ?? throw new InvalidOperationException("No guest found to confirm cart.");

        var guestSessionsApi = new GuestSessionsApi(Actor.UsingTheApi());
        var res = await guestSessionsApi.SubmitCartAsync(guest.SessionId, guest.SessionToken);
        if (!res.Ok)
            throw new InvalidOperationException($"Submitting cart for guest '{guest.GuestName}' failed ({res.Status}): {await res.TextAsync()}");

        guest.IsSubmitted = true;
    }

    [When("the owner refreshes the group pre-order")]
    public Task WhenOwnerRefreshesTheGroupPreOrder() =>
        Actor.AttemptsToAsync(new RefreshTheGroupPreOrder());

    [When("another customer asks for that group's pre-order sessions")]
    public Task WhenAnotherCustomerAsksForGroupSessions() =>
        Actor.AttemptsToAsync(new AskForGuestSessions(asRole: "another_customer"));

    [When("a waiter asks for that group's pre-order sessions")]
    public Task WhenWaiterAsksForGroupSessions() =>
        Actor.AttemptsToAsync(new AskForGuestSessions(asRole: "waiter"));

    [When("an unauthorized visitor asks for that group's pre-order sessions")]
    public Task WhenUnauthorizedVisitorAsksForGroupSessions() =>
        Actor.AttemptsToAsync(new AskForGuestSessions(asRole: "unauthorized"));

    [When("an unauthorized visitor tries to open that group's pre-order page")]
    public async Task WhenUnauthorizedVisitorOpensGroupOrderPage()
    {
        var context = Recall<GroupReservationContext>();
        var page = Actor.LookingAtThePage();
        await page.Context.ClearCookiesAsync();
        await page.EvaluateAsync("() => { try { localStorage.clear(); sessionStorage.clear(); } catch {} }");
        await new GroupOrderPage(page).NavigateAsync(context.Reservation.Id);
    }

    [When("the owner asks for a non-existent group's pre-order sessions")]
    public Task WhenOwnerAsksForNonExistentGroupSessions() =>
        Actor.AttemptsToAsync(new AskForGuestSessions(groupReservationId: "00000000-0000-0000-0000-000000000000"));

    [When("the owner opens a non-existent group pre-order page")]
    public Task WhenOwnerOpensNonExistentGroupPreOrderPage() =>
        Actor.AttemptsToAsync(new OpenTheGroupPreOrderPage(reservationId: "00000000-0000-0000-0000-000000000000"));

    [Then("the owner sees all participants with their dishes, quantities, and individual totals")]
    public async Task ThenOwnerSeesAllParticipantsWithDishesAndTotals()
    {
        var participants = await Actor.AsksForAsync(new TheGroupOrderParticipants());
        var context = Recall<GroupReservationContext>();

        await Assert.That(participants.Count).IsGreaterThanOrEqualTo(1)
            .Because("at least the owner must be listed as a participant");

        foreach (var guest in context.Guests)
        {
            var found = participants.FirstOrDefault(p => p.Name.Contains(guest.GuestName, StringComparison.OrdinalIgnoreCase));
            await Assert.That(found).IsNotNull()
                .Because($"participant '{guest.GuestName}' should be displayed on the page");

            if (guest.Dishes.Count > 0)
            {
                await Assert.That(found!.Items.Count).IsEqualTo(guest.Dishes.Count)
                    .Because($"participant '{guest.GuestName}' should have {guest.Dishes.Count} dish item(s)");
            }
        }
    }

    [Then("the grand total matches the sum of all participant totals")]
    public async Task ThenGrandTotalMatchesSumOfParticipantTotals()
    {
        var participants = await Actor.AsksForAsync(new TheGroupOrderParticipants());
        var grandTotal = await Actor.AsksForAsync(new TheGrandTotalShown());

        decimal sumOfSubtotals = 0m;
        foreach (var p in participants)
        {
            var match = Regex.Match(p.Subtotal, @"[\d.,]+");
            if (match.Success && decimal.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var subtotal))
            {
                sumOfSubtotals += subtotal;
            }
        }

        await Assert.That(grandTotal).IsEqualTo(sumOfSubtotals)
            .Because($"grand total ({grandTotal}) must equal the sum of all participant subtotals ({sumOfSubtotals})");
    }

    [Then("the grand total updates to reflect the new participant sum")]
    public async Task ThenGrandTotalUpdatesToReflectNewParticipantSum()
    {
        var page = Actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);

        var participants = await Actor.AsksForAsync(new TheGroupOrderParticipants());
        decimal sum = 0m;
        foreach (var p in participants)
        {
            var match = Regex.Match(p.Subtotal, @"[\d.,]+");
            if (match.Success && decimal.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var subtotal))
            {
                sum += subtotal;
            }
        }

        if (sum > 0m)
        {
            await Assertions.Expect(groupOrderPage.GrandTotal).ToContainTextAsync(sum.ToString("F2", CultureInfo.InvariantCulture), new() { Timeout = 7000 });
        }

        var grandTotal = await Actor.AsksForAsync(new TheGrandTotalShown());
        await Assert.That(grandTotal).IsEqualTo(sum)
            .Because($"dynamically updated grand total ({grandTotal}) must equal new sum of subtotals ({sum})");
    }

    [Then("the sessions list contains all participants with their items and subtotals")]
    public async Task ThenSessionsListContainsAllParticipantsWithItemsAndSubtotals()
    {
        var response = await Actor.AsksForAsync(new TheGuestSessionsResponse());
        var context = Recall<GroupReservationContext>();

        await Assert.That(response.Participants).IsNotNull();
        foreach (var guest in context.Guests)
        {
            var found = response.Participants.FirstOrDefault(p => p.Name.Equals(guest.GuestName, StringComparison.OrdinalIgnoreCase));
            await Assert.That(found).IsNotNull()
                .Because($"guest '{guest.GuestName}' must be present in the API response");

            if (guest.Dishes.Count > 0)
            {
                await Assert.That(found!.Items.Count).IsEqualTo(guest.Dishes.Count);
            }
        }
    }

    [Then("the total equals the sum of participant totals")]
    [Then("the grand total equals the updated sum of participant totals")]
    public async Task ThenApiTotalEqualsSumOfParticipantTotals()
    {
        var response = await Actor.AsksForAsync(new TheGuestSessionsResponse());
        var sum = response.Participants.Sum(p => p.Total);
        await Assert.That(response.Total).IsEqualTo(sum)
            .Because($"API total ({response.Total}) must equal sum of participant totals ({sum})");
    }

    [Then("the participant with an unconfirmed cart is marked as still choosing")]
    public async Task ThenParticipantWithUnconfirmedCartIsStillChoosing()
    {
        var participants = await Actor.AsksForAsync(new TheGroupOrderParticipants());
        var unconfirmed = participants.FirstOrDefault(p => p.Name.Contains("Bob", StringComparison.OrdinalIgnoreCase));

        await Assert.That(unconfirmed).IsNotNull();
        await Assert.That(unconfirmed!.Status).IsEqualTo("Still choosing")
            .Because("unconfirmed guest should have 'Still choosing' chip");
    }

    [Then("the participant with no dishes is displayed with an empty cart message")]
    public async Task ThenParticipantWithNoDishesHasEmptyCartMessage()
    {
        var participants = await Actor.AsksForAsync(new TheGroupOrderParticipants());
        var emptyGuest = participants.FirstOrDefault(p => p.Name.Contains("David", StringComparison.OrdinalIgnoreCase));

        await Assert.That(emptyGuest).IsNotNull();
        await Assert.That(emptyGuest!.HasEmptyDishes).IsTrue()
            .Because("guest with no dishes should display empty dishes message");
    }

    [Then("the participant with an unconfirmed cart has unsubmitted status")]
    public async Task ThenParticipantWithUnconfirmedCartHasUnsubmittedStatus()
    {
        var response = await Actor.AsksForAsync(new TheGuestSessionsResponse());
        var participant = response.Participants.FirstOrDefault(p => p.Name.Equals("Bob", StringComparison.OrdinalIgnoreCase));

        await Assert.That(participant).IsNotNull();
        await Assert.That(participant!.IsSubmitted).IsFalse()
            .Because("unconfirmed participant should have isSubmitted = false");
    }

    [Then("the participant with no dishes has no items and a zero total")]
    public async Task ThenParticipantWithNoDishesHasNoItemsAndZeroTotal()
    {
        var response = await Actor.AsksForAsync(new TheGuestSessionsResponse());
        var participant = response.Participants.FirstOrDefault(p => p.Name.Equals("David", StringComparison.OrdinalIgnoreCase));

        await Assert.That(participant).IsNotNull();
        await Assert.That(participant!.Items.Count).IsEqualTo(0);
        await Assert.That(participant.Total).IsEqualTo(0m);
    }

    [Then("the owner sees no participant cards")]
    public async Task ThenOwnerSeesNoParticipantCards()
    {
        var page = Actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);
        await Assertions.Expect(groupOrderPage.Participants).ToHaveCountAsync(0);
    }

    [Then("the owner sees the empty pre-order banner")]
    public async Task ThenOwnerSeesEmptyPreOrderBanner()
    {
        var isShown = await Actor.AsksForAsync(new TheEmptyDishesBannerIsShown());
        await Assert.That(isShown).IsTrue()
            .Because("empty dishes banner should be visible when no dishes have been added");
    }

    [Then("the grand total is zero")]
    public async Task ThenGrandTotalIsZero()
    {
        var total = await Actor.AsksForAsync(new TheGrandTotalShown());
        await Assert.That(total).IsEqualTo(0m)
            .Because("grand total should be 0 for an empty group pre-order");
    }

    [Then("the group has no guest participants and a zero total")]
    public async Task ThenGroupHasNoGuestParticipantsAndZeroTotal()
    {
        var response = await Actor.AsksForAsync(new TheGuestSessionsResponse());
        var guests = response.Participants.Where(p => p.Type.Equals("Guest", StringComparison.OrdinalIgnoreCase)).ToList();

        await Assert.That(guests.Count).IsEqualTo(0)
            .Because("an empty group should have 0 joined guests");
        await Assert.That(response.Total).IsEqualTo(0m)
            .Because("grand total should be 0 for an empty group");
    }

    [Then("the request is forbidden with an owner-only restriction message")]
    public async Task ThenRequestIsForbiddenWithOwnerOnlyMessage()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(403)
            .Because($"expected HTTP 403 Forbidden, but received {outcome.Status}: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Contains("Only the reservation owner")
            .Because("backend should forbid non-owner access to group sessions or pre-orders");
    }

    [Then("the request is refused as unauthorized")]
    public async Task ThenRequestIsRefusedAsUnauthorized()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(401)
            .Because($"expected HTTP 401 Unauthorized, but received {outcome.Status}: {outcome.Body}");
    }

    [Then("the visitor is redirected to the sign-in page")]
    public async Task ThenVisitorIsRedirectedToSignInPage()
    {
        var page = Actor.LookingAtThePage();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@".*/login"), new() { Timeout = 10000 });
    }

    [Then("the group is not found")]
    public async Task ThenGroupIsNotFound()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(404)
            .Because($"expected HTTP 404 Not Found, but received {outcome.Status}: {outcome.Body}");
    }

    [Then("the group reservation not found message is shown")]
    public async Task ThenGroupReservationNotFoundMessageIsShown()
    {
        var error = await Actor.AsksForAsync(new TheGroupOrderErrorIsShown());
        await Assert.That(error.IsShown).IsTrue()
            .Because("error container should be displayed for non-existent group reservation");
        await Assert.That(error.Message).Contains("Group reservation not found")
            .Because("not found error message should be displayed");
    }
}

