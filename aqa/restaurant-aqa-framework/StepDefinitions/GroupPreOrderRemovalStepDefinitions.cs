using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GuestSessions;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Ui;
using Reqnroll;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class GroupPreOrderRemovalStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [When("the owner removes the guest {string}")]
    public Task WhenOwnerRemovesGuestInUi(string guestName) =>
        Actor.AttemptsToAsync(new RemoveGuestFromGroupInUi(guestName));

    [When("the owner removes the guest {string} through the service")]
    public Task WhenOwnerRemovesGuestThroughService(string guestName) =>
        Actor.AttemptsToAsync(new RemoveGuestThroughTheApi(guestName: guestName));

    [When("another customer attempts to remove the guest {string} through the service")]
    public Task WhenAnotherCustomerAttemptsToRemoveGuest(string guestName) =>
        Actor.AttemptsToAsync(new RemoveGuestThroughTheApi(guestName: guestName, asRole: "another_customer"));

    [When("a waiter attempts to remove the guest {string} through the service")]
    public Task WhenWaiterAttemptsToRemoveGuest(string guestName) =>
        Actor.AttemptsToAsync(new RemoveGuestThroughTheApi(guestName: guestName, asRole: "waiter"));

    [When("an unauthorized visitor attempts to remove the guest {string} through the service")]
    public Task WhenUnauthorizedVisitorAttemptsToRemoveGuest(string guestName) =>
        Actor.AttemptsToAsync(new RemoveGuestThroughTheApi(guestName: guestName, asRole: "unauthorized"));

    [When("the owner attempts to remove a non-existent guest session")]
    public Task WhenOwnerAttemptsToRemoveNonExistentGuestSession() =>
        Actor.AttemptsToAsync(new RemoveGuestThroughTheApi(customGuestSessionId: "00000000-0000-0000-0000-000000000000"));

    [When("the owner attempts to remove their own session from the group order")]
    public async Task WhenOwnerAttemptsToRemoveOwnSession()
    {
        var context = Recall<GroupReservationContext>();
        var guestSessionsApi = new GuestSessionsApi(Actor.UsingTheApi());
        var res = await guestSessionsApi.GetOwnerSessionIdAsync(context.Reservation.Id, context.OwnerBearerToken);
        var ownerSessionId = (await res.ReadAsAsync<GetOwnerSessionIdResponse>()).GuestSessionId;

        await Actor.AttemptsToAsync(new RemoveGuestThroughTheApi(customGuestSessionId: ownerSessionId));
    }

    [Given("another independent group pre-order has been set up with guest {string}")]
    public Task GivenAnotherIndependentGroupSetUpWithGuest(string guestName) =>
        Actor.AttemptsToAsync(new SetUpAnotherGroupReservation(guestName));

    [When("the owner attempts to remove the guest {string} from their group")]
    public Task WhenOwnerAttemptsToRemoveMismatchedGuest(string guestName)
    {
        var firstContext = Actor.Recall<GroupReservationContext>();
        var secondContext = Actor.Recall<GroupReservationContext>(SetUpAnotherGroupReservation.SecondGroupKey);
        var secondGuest = secondContext.Guests.First(g => g.GuestName.Equals(guestName, StringComparison.OrdinalIgnoreCase));

        return Actor.AttemptsToAsync(new RemoveGuestThroughTheApi(
            customGroupReservationId: firstContext.GroupReservationId,
            customGuestSessionId: secondGuest.SessionId));
    }

    [Then("the guest {string} is no longer listed as a participant")]
    public async Task ThenGuestNoLongerListedInUi(string guestName)
    {
        var page = Actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);
        var participantCard = groupOrderPage.ParticipantByName(guestName);
        await Assertions.Expect(participantCard.Root).ToHaveCountAsync(0);
    }

    [Then("the guest {string} remains listed as a participant")]
    public async Task ThenGuestRemainsListedInUi(string guestName)
    {
        var page = Actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);
        var participantCard = groupOrderPage.ParticipantByName(guestName);
        await Assertions.Expect(participantCard.Root).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Then("the grand total is recalculated to match the remaining participants")]
    public async Task ThenGrandTotalRecalculatedInUi()
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
            .Because("UI grand total must match the sum of remaining participants' subtotals");
    }

    [Then("the sessions list contains only {string} and the owner")]
    public async Task ThenSessionsListContainsOnlyRemainingGuestAndOwner(string remainingGuestName)
    {
        var sessions = await Actor.AsksForAsync(new TheGuestSessionsResponse());
        var guestNames = sessions.Participants.Where(p => p.Type.Equals("Guest", StringComparison.OrdinalIgnoreCase)).Select(p => p.Name).ToList();

        await Assert.That(guestNames.Count).IsEqualTo(1);
        await Assert.That(guestNames[0]).Contains(remainingGuestName);
        await Assert.That(sessions.Participants.Any(p => p.Type.Equals("Owner", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Then("and the grand total reflects only the remaining participants")]
    [Then("the grand total reflects only the remaining participants")]
    public async Task ThenGrandTotalReflectsRemainingParticipantsInApi()
    {
        var sessions = await Actor.AsksForAsync(new TheGuestSessionsResponse());
        var sum = sessions.Participants.Sum(p => p.Total);
        await Assert.That(sessions.Total).IsEqualTo(sum);
    }

    [Then("the guest {string} is removed successfully")]
    public async Task ThenGuestRemovedSuccessfully(string guestName)
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(200);
    }

    [Then("no guest participants remain in the group pre-order")]
    public async Task ThenNoGuestParticipantsRemainInUi()
    {
        var participants = await Actor.AsksForAsync(new TheGroupOrderParticipants());
        var guestsOnly = participants.Where(p =>
            !p.Name.Contains("My Pre-Order", StringComparison.OrdinalIgnoreCase) &&
            !p.Name.Contains("(You)", StringComparison.OrdinalIgnoreCase) &&
            !p.Name.Contains("Owner", StringComparison.OrdinalIgnoreCase) &&
            !p.Name.Contains("Host", StringComparison.OrdinalIgnoreCase)).ToList();
        await Assert.That(guestsOnly.Count).IsEqualTo(0);
    }

    [Then("the empty pre-order banner is displayed")]
    public async Task ThenEmptyPreOrderBannerDisplayed()
    {
        var isShown = await Actor.AsksForAsync(new TheEmptyDishesBannerIsShown());
        await Assert.That(isShown).IsTrue().Because("empty dishes banner should be visible when no dishes exist");
    }

    [Then("the sessions response contains no guest participants and zero grand total")]
    public async Task ThenSessionsResponseContainsNoGuestsAndZeroTotal()
    {
        var sessions = await Actor.AsksForAsync(new TheGuestSessionsResponse());
        var guests = sessions.Participants.Where(p => p.Type.Equals("Guest", StringComparison.OrdinalIgnoreCase)).ToList();
        await Assert.That(guests.Count).IsEqualTo(0);
        await Assert.That(sessions.Total).IsEqualTo(0m);
    }

    [Then("the removal is refused because the guest belongs to another group")]
    public async Task ThenRemovalRefusedGuestBelongsToAnotherGroup()
    {
        var response = Actor.TheLastResponse();
        if (response.Status != 400)
        {
            throw new InvalidOperationException($"Expected status 400 but received {response.Status}. Response Body: '{response.Body}'");
        }
        await Assert.That(response.Status).IsEqualTo(400);
        await Assert.That(response.Body).Contains("does not belong to this group reservation");
    }

    [Then("the guest session is not found")]
    public async Task ThenGuestSessionNotFound()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(404);
    }

    [Then("the removal is refused because the owner session cannot be removed")]
    public async Task ThenRemovalRefusedOwnerSessionCannotBeRemoved()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(400);
        await Assert.That(response.Body).Contains("Cannot remove the owner session");
    }

    [Then("the removal request is forbidden with an owner-only restriction message")]
    public async Task ThenRemovalRequestForbidden()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(403);
        await Assert.That(response.Body).Contains("Only the reservation owner");
    }

    [Then("the removal request is refused as unauthorized")]
    public async Task ThenRemovalRequestUnauthorized()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(401);
    }

    [Then("the order does not contain dishes from the removed guest {string}")]
    public async Task ThenOrderDoesNotContainDishesFromRemovedGuest(string removedGuestName)
    {
        var page = Actor.LookingAtThePage();
        var waiterPage = new WaiterPreOrderPage(page);
        var items = await waiterPage.GetPreOrderItemsAsync();

        var caesarItem = items.FirstOrDefault(i => i.DishName.Contains("Caesar", StringComparison.OrdinalIgnoreCase) || i.DishName.Contains("Salad", StringComparison.OrdinalIgnoreCase));
        await Assert.That(caesarItem.DishName).IsNullOrEmpty().Because("removed guest's Caesar Salad must be excluded from confirmed waiter order");
    }
}
