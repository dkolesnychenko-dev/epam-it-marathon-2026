@group-preorder
Feature: Group pre-order guest sessions list

  The reservation owner can invite guests to join their group pre-order, see who has joined,
  which dishes each guest selected, individual subtotals, and the consolidated grand total.

  Rule: The owner can view all joined participants and their selected dishes

    @ui @level-1
    Scenario: The owner views participants, their dishes, quantities, and individual totals
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x2 | submitted |
        | Claire | Caesar Salad x1 | submitted |
      When the owner reviews the group pre-order
      Then the owner sees all participants with their dishes, quantities, and individual totals
      And the grand total matches the sum of all participant totals

    @api @level-1
    Scenario: The owner retrieves the list of participants and totals through the service
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x2 | submitted |
      When the owner asks for the group pre-order sessions
      Then the sessions list contains all participants with their items and subtotals
      And the total equals the sum of participant totals

  Rule: The grand total dynamically reflects changes to participants' carts

    @ui @level-1
    Scenario: The grand total updates when a guest modifies their cart
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x1 | choosing  |
      And the owner is viewing the group pre-order
      When the guest modifies their cart by adding another dish
      And the guest confirms their cart
      And the owner refreshes the group pre-order
      Then the grand total updates to reflect the new participant sum

    @api @level-1
    Scenario: The service recalculates totals when a guest adds dishes
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x1 | choosing  |
      When the guest adds another dish to their cart
      And the owner asks for the group pre-order sessions
      Then the grand total equals the updated sum of participant totals

  Rule: Participants' cart confirmation status and empty carts are clearly indicated

    @ui @level-1
    Scenario: Unconfirmed carts show choosing status and empty carts show no dishes
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Caesar Salad x1 | choosing  |
        | David  | (none)          | choosing  |
      When the owner reviews the group pre-order
      Then the participant with an unconfirmed cart is marked as still choosing
      And the participant with no dishes is displayed with an empty cart message

    @api @level-1
    Scenario: Unsubmitted carts and empty dishes are returned in the sessions response
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Caesar Salad x1 | choosing  |
        | David  | (none)          | choosing  |
      When the owner asks for the group pre-order sessions
      Then the participant with an unconfirmed cart has unsubmitted status
      And the participant with no dishes has no items and a zero total

  Rule: An empty group displays zero total and an empty state

    @ui @level-1
    Scenario: The owner opens an empty group pre-order where no guests have joined
      Given an owner has booked a table for a group with no guests joined
      When the owner reviews the group pre-order
      Then the owner sees the empty pre-order banner
      And the grand total is zero

    @api @level-1
    Scenario: The service returns an empty participants list and zero total for an empty group
      Given an owner has booked a table for a group with no guests joined
      When the owner asks for the group pre-order sessions
      Then the group has no guest participants and a zero total

  Rule: Only the reservation owner can view the group pre-order sessions

    @api @negative @level-1
    Scenario: Another customer cannot view someone else's group pre-order sessions
      Given an owner has booked a table for a group
      When another customer asks for that group's pre-order sessions
      Then the request is forbidden with an owner-only restriction message

    @api @negative @level-1
    Scenario: A waiter cannot view the customer's group pre-order sessions
      Given an owner has booked a table for a group
      When a waiter asks for that group's pre-order sessions
      Then the request is forbidden with an owner-only restriction message

    @api @negative @level-1
    Scenario: An unauthorized visitor cannot view group pre-order sessions
      Given a group pre-order exists
      When an unauthorized visitor asks for that group's pre-order sessions
      Then the request is refused as unauthorized

    @ui @negative @level-1
    Scenario: An unauthorized visitor is redirected away from the group pre-order page
      Given a group pre-order exists
      When an unauthorized visitor tries to open that group's pre-order page
      Then the visitor is redirected to the sign-in page

  Rule: A non-existent group pre-order cannot be viewed

    @api @negative @level-1
    Scenario: Asking for a non-existent group pre-order returns not found
      Given an owner has booked a table for a group
      When the owner asks for a non-existent group's pre-order sessions
      Then the group is not found

    @ui @negative @level-1
    Scenario: Opening a non-existent group pre-order shows a not found message
      Given an owner has booked a table for a group
      When the owner opens a non-existent group pre-order page
      Then the group reservation not found message is shown
