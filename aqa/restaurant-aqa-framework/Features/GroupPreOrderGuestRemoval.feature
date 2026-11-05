@group-preorder
Feature: Group pre-order guest session removal

  The reservation owner can remove a participant's session from the group order.
  Afterward, they see the updated participant list and recalculated total amount.
  Only a participant who belongs to this specific group can be removed, and only if they exist.
  Only the owner can remove participants.

  Rule: The owner can remove participants from the group pre-order

    @ui @level-3
    Scenario: The owner removes a participant from the group pre-order in the UI
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x2 | submitted |
        | Claire | Salmon Steak x1 | submitted |
      And the owner is viewing the group pre-order
      When the owner removes the guest "Bob"
      Then the guest "Bob" is no longer listed as a participant
      And the guest "Claire" remains listed as a participant
      And the grand total is recalculated to match the remaining participants

    @api @level-3
    Scenario: The owner removes a participant via the service
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status   |
        | Bob    | Salmon Steak x2 | choosing |
        | Claire | Salmon Steak x1 | choosing |
      When the owner removes the guest "Bob" through the service
      Then the sessions list contains only "Claire" and the owner
      And the grand total reflects only the remaining participants

    @api @level-3
    Scenario: The owner removes a participant who has already confirmed their cart
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x1 | submitted |
        | Claire | Salmon Steak x1 | choosing  |
      When the owner removes the guest "Bob" through the service
      Then the guest "Bob" is removed successfully
      And the sessions list contains only "Claire" and the owner

    @ui @level-3
    Scenario: The owner removes the last remaining participant in the UI
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x1 | submitted |
      And the owner is viewing the group pre-order
      When the owner removes the guest "Bob"
      Then no guest participants remain in the group pre-order
      And the empty pre-order banner is displayed

    @api @level-3
    Scenario: The owner removes the last remaining participant via the service
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status   |
        | Bob    | Salmon Steak x1 | choosing |
      When the owner removes the guest "Bob" through the service
      Then the guest "Bob" is removed successfully
      And the sessions response contains no guest participants and zero grand total

  Rule: Participant removal is restricted to existing participants belonging to this group

    @api @negative @level-3
    Scenario: Removing a participant belonging to another group is refused
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status   |
        | Bob    | Salmon Steak x1 | choosing |
      And another independent group pre-order has been set up with guest "Dave"
      When the owner attempts to remove the guest "Dave" from their group
      Then the removal is refused because the guest belongs to another group

    @api @negative @level-3
    Scenario: Removing a non-existent participant returns not found
      Given an owner has booked a table for a group
      When the owner attempts to remove a non-existent guest session
      Then the guest session is not found

    @api @negative @level-3
    Scenario: Removing the owner session is refused
      Given an owner has booked a table for a group
      When the owner attempts to remove their own session from the group order
      Then the removal is refused because the owner session cannot be removed

  Rule: Only the reservation owner can remove participants

    @api @negative @level-3
    Scenario: Another customer cannot remove a participant from the group pre-order
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status   |
        | Bob    | Salmon Steak x1 | choosing |
      When another customer attempts to remove the guest "Bob" through the service
      Then the removal request is forbidden with an owner-only restriction message

    @api @negative @level-3
    Scenario: A waiter cannot remove a participant from the group pre-order
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status   |
        | Bob    | Salmon Steak x1 | choosing |
      When a waiter attempts to remove the guest "Bob" through the service
      Then the removal request is forbidden with an owner-only restriction message

    @api @negative @level-3
    Scenario: An unauthorized visitor cannot remove a participant from the group pre-order
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status   |
        | Bob    | Salmon Steak x1 | choosing |
      When an unauthorized visitor attempts to remove the guest "Bob" through the service
      Then the removal request is refused as unauthorized

  Rule: End-to-end guest removal reflected in confirmed restaurant order

    @ui @level-3
    Scenario: Guests add dishes, owner removes one, confirms pre-order, and waiter sees confirmed order without the removed guest
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x2 | submitted |
        | Claire | Salmon Steak x1 | submitted |
        | Dave   | Caesar Salad x1 | submitted |
      And the owner is viewing the group pre-order
      When the owner removes the guest "Dave"
      And the owner submits the consolidated pre-order
      And the waiter views the pre-order for this reservation
      Then the waiter sees all confirmed items with exact quantities
      And the order does not contain dishes from the removed guest "Dave"

