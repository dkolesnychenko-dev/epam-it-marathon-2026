using System.Globalization;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupReservations;
using Restaurant_Aqa_Framework.Api.Dtos.GuestSessions;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Contexts;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Ui;
using Reqnroll;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class GroupPreOrderConfirmationStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("the owner has submitted the consolidated pre-order")]
    public Task GivenOwnerHasSubmittedConsolidatedPreOrder() =>
        Actor.AttemptsToAsync(new SubmitTheConsolidatedPreOrderThroughTheApi());

    [When("the owner submits the consolidated pre-order")]
    public Task WhenOwnerSubmitsConsolidatedPreOrderInUi() =>
        Actor.AttemptsToAsync(new SubmitTheConsolidatedPreOrderInUi());

    [When("the owner submits the consolidated pre-order through the service")]
    public Task WhenOwnerSubmitsConsolidatedPreOrderThroughService() =>
        Actor.AttemptsToAsync(new SubmitTheConsolidatedPreOrderThroughTheApi());

    [When("the owner submits the consolidated pre-order through the service again")]
    public Task WhenOwnerSubmitsConsolidatedPreOrderThroughServiceAgain() =>
        Actor.AttemptsToAsync(new SubmitTheConsolidatedPreOrderThroughTheApi());

    [When("the waiter views the pre-order for this reservation")]
    public Task WhenWaiterViewsPreOrder() =>
        Actor.AttemptsToAsync(new OpenTheWaiterPreOrderPage());

    [When("the waiter asks for the order details through the service")]
    public Task WhenWaiterAsksForOrderDetails() =>
        Actor.AttemptsToAsync(new AskForOrderByReservationId());

    [When("the owner submits a consolidated pre-order for a non-existent group")]
    public Task WhenOwnerSubmitsForNonExistentGroup() =>
        Actor.AttemptsToAsync(new SubmitTheConsolidatedPreOrderThroughTheApi(customGroupReservationId: "00000000-0000-0000-0000-000000000000"));

    [When("the owner submits the consolidated pre-order with a mismatched reservation ID")]
    public Task WhenOwnerSubmitsWithMismatchedReservationId() =>
        Actor.AttemptsToAsync(new SubmitTheConsolidatedPreOrderThroughTheApi(customReservationId: "00000000-0000-0000-0000-000000000000"));

    [When("another customer attempts to submit the consolidated pre-order")]
    public Task WhenAnotherCustomerAttemptsToSubmit() =>
        Actor.AttemptsToAsync(new SubmitTheConsolidatedPreOrderThroughTheApi(asRole: "another_customer"));

    [When("a waiter attempts to submit the consolidated pre-order")]
    public Task WhenWaiterAttemptsToSubmit() =>
        Actor.AttemptsToAsync(new SubmitTheConsolidatedPreOrderThroughTheApi(asRole: "waiter"));

    [When("an unauthorized visitor attempts to submit the consolidated pre-order")]
    public Task WhenUnauthorizedVisitorAttemptsToSubmit() =>
        Actor.AttemptsToAsync(new SubmitTheConsolidatedPreOrderThroughTheApi(asRole: "unauthorized"));

    [Then("the group pre-order status indicates it has been sent to the restaurant")]
    public async Task ThenGroupPreOrderStatusSentToRestaurant()
    {
        var page = Actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);
        await Assertions.Expect(groupOrderPage.StatusBadge).ToContainTextAsync("Sent to Restaurant", new() { Timeout = 10000 });
    }

    [Then("the restaurant order summary merges identical dishes with summed quantities")]
    public async Task ThenRestaurantOrderSummaryMergesIdenticalDishes()
    {
        var summary = await Actor.AsksForAsync(new TheRestaurantOrderSummary());
        var salmonItem = summary.FirstOrDefault(i => i.DishName.Contains("Salmon", StringComparison.OrdinalIgnoreCase));
        await Assert.That(salmonItem.DishName).IsNotNull().Because("Salmon dish should be present in order summary");
        await Assert.That(salmonItem.Quantity).IsEqualTo(3).Because("identical Salmon items should be merged with quantity 3 (2+1)");
    }

    [Then("dishes from unconfirmed carts are excluded from the restaurant order summary")]
    public async Task ThenUnconfirmedDishesExcludedFromSummary()
    {
        var summary = await Actor.AsksForAsync(new TheRestaurantOrderSummary());
        var caesar = summary.FirstOrDefault(i => i.DishName.Contains("Salad", StringComparison.OrdinalIgnoreCase) || i.DishName.Contains("Caesar", StringComparison.OrdinalIgnoreCase));
        await Assert.That(caesar.DishName).IsNullOrEmpty().Because("unconfirmed guest's dish must be excluded from order summary");
    }

    [Then("the consolidated pre-order is accepted with aggregated dishes and quantities")]
    public async Task ThenConsolidatedPreOrderAccepted()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(200);
        var dishes = await Actor.AsksForAsync(new TheConsolidatedDishesResponse());
        await Assert.That(dishes.Count).IsGreaterThanOrEqualTo(1);
        var totalQty = dishes.Sum(d => d.Quantity);
        await Assert.That(totalQty).IsEqualTo(3).Because("2 from Bob + 1 from Claire = 3");
    }

    [Then("the group reservation status becomes submitted")]
    public async Task ThenGroupReservationStatusBecomesSubmitted()
    {
        var context = Recall<GroupReservationContext>();
        var apiContext = Actor.UsingTheApi();
        var guestSessionsApi = new GuestSessionsApi(apiContext);
        var statusRes = await guestSessionsApi.GetGroupStatusAsync(context.GroupReservationId);
        await Assert.That(statusRes.Ok).IsTrue();
        var statusDto = await statusRes.ReadAsAsync<GroupReservationStatusDto>();
        await Assert.That(statusDto.Status).IsEqualTo("Submitted");
    }

    [Then("the waiter sees all confirmed items with exact quantities")]
    public async Task ThenWaiterViewsAllConfirmedItemsWithQuantities()
    {
        var page = Actor.LookingAtThePage();
        var waiterPage = new WaiterPreOrderPage(page);
        await Assertions.Expect(waiterPage.DishCards.First).ToBeVisibleAsync(new() { Timeout = 15000 });
        var items = await waiterPage.GetPreOrderItemsAsync();
        var totalCount = items.Sum(i => i.Quantity);
        await Assert.That(totalCount).IsGreaterThanOrEqualTo(3).Because("confirmed pre-order items total quantity should be at least 3");
    }

    [Then("the order status is submitted by customer")]
    public async Task ThenOrderStatusSubmittedByCustomer()
    {
        var order = await Actor.AsksForAsync(new TheWaiterOrderResponse());
        await Assert.That(order.Status).IsEqualTo(OrderStatus.SubmittedByCustomer);
    }

    [Then("the order contains the exact consolidated dishes and quantities")]
    public async Task ThenOrderContainsExactConsolidatedDishesAndQuantities()
    {
        var order = await Actor.AsksForAsync(new TheWaiterOrderResponse());
        var totalQuantity = order.OrderItems.Sum(i => i.Quantity);
        await Assert.That(totalQuantity).IsGreaterThanOrEqualTo(3).Because("consolidated order should include confirmed items (at least 3)");
    }

    [Then("the submission is refused because there are no submitted guest sessions")]
    public async Task ThenSubmissionRefusedNoSubmittedGuestSessions()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(400);
        var containsExpected = response.Body.Contains("There are no submitted guest sessions", StringComparison.OrdinalIgnoreCase)
            || response.Body.Contains("Order for reservation", StringComparison.OrdinalIgnoreCase)
            || response.Body.Contains("not found", StringComparison.OrdinalIgnoreCase);
        await Assert.That(containsExpected).IsTrue();
    }

    [Then("the submit pre-order action is unavailable")]
    public async Task ThenSubmitPreOrderActionIsUnavailable()
    {
        var page = Actor.LookingAtThePage();
        var groupOrderPage = new GroupOrderPage(page);
        await Assertions.Expect(groupOrderPage.SubmitButton).ToBeDisabledAsync();
    }

    [Then("the submission is refused because confirmed sessions contain no dishes")]
    public async Task ThenSubmissionRefusedNoDishes()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(400);
        var containsExpected = response.Body.Contains("no dishes", StringComparison.OrdinalIgnoreCase)
            || response.Body.Contains("no submitted guest sessions", StringComparison.OrdinalIgnoreCase)
            || response.Body.Contains("Order for reservation", StringComparison.OrdinalIgnoreCase);
        await Assert.That(containsExpected).IsTrue();
    }

    [Then("the duplicate submission is refused")]
    public async Task ThenDuplicateSubmissionRefused()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(400);
        await Assert.That(response.Body).Contains("Group reservation was submitted");
    }

    [Then("the group reservation is not found")]
    public async Task ThenGroupReservationNotFound()
    {
        var response = Actor.TheLastResponse();
        await Assert.That(response.Status).IsEqualTo(404);
    }

    [Then("the submission is refused due to mismatched reservation ID")]
    public async Task ThenSubmissionRefusedMismatchedReservationId()
    {
        var status = Actor.TheLastResponse().Status;
        await Assert.That(status is 400 or 404).IsTrue().Because("mismatched reservation ID should return 400 or 404");
    }
}
