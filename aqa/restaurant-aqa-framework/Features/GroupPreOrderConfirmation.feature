@group-preorder
Feature: Group pre-order confirmation and consolidated submit

  The reservation owner can confirm the consolidated group pre-order to submit
  all confirmed guest dishes as a single consolidated order to the restaurant.
  Identical dishes from different guests are merged and summed, while unconfirmed
  carts are excluded from the restaurant order.

  Rule: The owner can confirm the consolidated pre-order to send it to the restaurant

    @ui @level-2
    Scenario: The owner confirms the consolidated pre-order with merged identical dishes
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x2 | submitted |
        | Claire | Salmon Steak x1 | submitted |
        | Dave   | Caesar Salad x1 | choosing  |
      And the owner is viewing the group pre-order
      When the owner submits the consolidated pre-order
      Then the group pre-order status indicates it has been sent to the restaurant
      And the restaurant order summary merges identical dishes with summed quantities
      And dishes from unconfirmed carts are excluded from the restaurant order summary

    @api @level-2
    Scenario: The owner confirms the consolidated pre-order via the service
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x2 | submitted |
        | Claire | Salmon Steak x1 | submitted |
      When the owner submits the consolidated pre-order through the service
      Then the consolidated pre-order is accepted with aggregated dishes and quantities
      And the group reservation status becomes submitted

  Rule: The waiter can view the owner confirmed consolidated order

    @ui @level-2
    Scenario: The waiter views the owner confirmed consolidated pre-order
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x2 | submitted |
        | Claire | Salmon Steak x1 | submitted |
      And the owner has submitted the consolidated pre-order
      When the waiter views the pre-order for this reservation
      Then the waiter sees all confirmed items with exact quantities

    @api @level-2
    Scenario: The waiter retrieves the confirmed order details via the service
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x2 | submitted |
        | Claire | Salmon Steak x1 | submitted |
      And the owner has submitted the consolidated pre-order
      When the waiter asks for the order details through the service
      Then the order status is submitted by customer
      And the order contains the exact consolidated dishes and quantities

  Rule: Submitting is restricted when carts or dishes are missing

    @api @negative @level-2
    Scenario: Submitting is refused when no guest carts are confirmed
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x1 | choosing  |
      When the owner submits the consolidated pre-order through the service
      Then the submission is refused because there are no submitted guest sessions

    @ui @negative @level-2
    Scenario: The owner cannot submit a pre-order when no confirmed dishes exist
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x1 | choosing  |
      When the owner reviews the group pre-order
      Then the submit pre-order action is unavailable

    @api @negative @level-2
    Scenario: Submitting is refused when confirmed carts contain no dishes
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes | status    |
        | Bob    | (none) | submitted |
      When the owner submits the consolidated pre-order through the service
      Then the submission is refused because confirmed sessions contain no dishes

    @api @negative @level-2
    Scenario: Submitting an already confirmed group pre-order a second time is refused
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x1 | submitted |
      And the owner has submitted the consolidated pre-order
      When the owner submits the consolidated pre-order through the service again
      Then the duplicate submission is refused

    @api @negative @level-2
    Scenario: Submitting for a non-existent group reservation returns not found
      Given an owner has booked a table for a group
      When the owner submits a consolidated pre-order for a non-existent group
      Then the group reservation is not found

    @api @negative @level-2
    Scenario: Submitting with a mismatched reservation ID is refused
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x1 | submitted |
      When the owner submits the consolidated pre-order with a mismatched reservation ID
      Then the submission is refused due to mismatched reservation ID

  Rule: Only the reservation owner can confirm and submit the consolidated pre-order

    @api @negative @level-2
    Scenario: A non-owner cannot submit the consolidated pre-order
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x1 | submitted |
      When another customer attempts to submit the consolidated pre-order
      Then the request is forbidden with an owner-only restriction message

    @api @negative @level-2
    Scenario: A waiter cannot submit the consolidated pre-order
      Given an owner has booked a table for a group
      And the following guests have joined the group pre-order:
        | name   | dishes          | status    |
        | Bob    | Salmon Steak x1 | submitted |
      When a waiter attempts to submit the consolidated pre-order
      Then the request is forbidden with an owner-only restriction message

    @api @negative @level-2
    Scenario: An unauthorized visitor cannot submit the consolidated pre-order
      Given an owner has booked a table for a group
      When an unauthorized visitor attempts to submit the consolidated pre-order
      Then the request is refused as unauthorized

