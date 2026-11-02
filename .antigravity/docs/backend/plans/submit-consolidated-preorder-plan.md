# Implementation Plan: Level 2 (Part 2) - Submit Consolidated Pre-order API

## 1. Goal & Overview
Implement the endpoint **`POST /group-reservations/{groupReservationId}/submit`** in `restaurant-backend-app` (.NET 10, AWS Lambda `api-handler`).
This endpoint allows the reservation owner to review and finalize the pre-order for a group reservation. When invoked:
1. The system verifies owner authorization and existence of the reservation, group reservation, and pre-order.
2. Identifies all participant guest sessions marked as `isSubmitted = true`.
3. Consolidates all dish items across submitted guest sessions, aggregating quantities by `dishId`.
4. Merges/adds the consolidated dishes into the active `Order`.
5. Updates the order status to `SubmittedByCustomer`.
6. Updates the group reservation status to `Submitted`.
7. Persists updates to DynamoDB and returns the consolidated list of dishes with their merged quantities.

---

## 2. API Contract Specification

- **Endpoint Path:** `/group-reservations/{groupReservationId}/submit`
- **HTTP Method:** `POST`
- **Authorization:** `cognito_authorizer` (Bearer JWT in `Authorization` header)
- **Path Parameters:**
  - `groupReservationId` (string, required): The unique identifier of the group reservation.
- **Request Body:**
  ```json
  {
    "reservationId": "reservation-123"
  }
  ```

### HTTP Responses:
- **`200 OK`**: Successfully submitted consolidated pre-order. Returns JSON array of consolidated dishes:
  ```json
  [
    {
      "dishId": "dish1",
      "quantity": 3
    },
    {
      "dishId": "dish2",
      "quantity": 2
    }
  ]
  ```
- **`400 Bad Request`**:
  - Missing or empty `groupReservationId` in path or `reservationId` in request body.
  - Invalid JSON body.
  - No `submitted` guest sessions exist for the group reservation.
  - Submitted guest sessions contain no dishes (or total quantity is zero).
  - No pre-order found for the reservation.
  - Mismatch between `groupReservation.ReservationId` and provided `reservationId`.
- **`401 Unauthorized`**: Caller is unauthenticated or `sub` claim is missing.
- **`403 Forbidden`**: Caller is not the owner of the reservation (`reservation.CustomerId != userId`).
- **`404 Not Found`**:
  - Reservation not found (`ReservationNotFoundException`).
  - Group reservation not found (`GroupReservationNotFoundException`).

---

## 3. Architecture & Component Breakdown

### 3.1 DTOs & Models (`APIHandler.Models.GroupReservation`)
Create two new record DTOs:
1. **`SubmitConsolidatedPreOrderRequest.cs`**:
   ```csharp
   using System.Text.Json.Serialization;

   namespace APIHandler.Models.GroupReservation;

   public record SubmitConsolidatedPreOrderRequest(
       [property: JsonPropertyName("reservationId")] string? ReservationId);
   ```
2. **`ConsolidatedDishResponse.cs`**:
   ```csharp
   using System.Text.Json.Serialization;

   namespace APIHandler.Models.GroupReservation;

   public record ConsolidatedDishResponse(
       [property: JsonPropertyName("dishId")] string DishId,
       [property: JsonPropertyName("quantity")] int Quantity);
   ```

### 3.2 Service Layer (`APIHandler.Services.GroupReservations`)
- **Interface Extension (`IGroupReservationService.cs`):**
  Add method signature:
  ```csharp
  Task<List<ConsolidatedDishResponse>> SubmitConsolidatedPreOrderAsync(
      string userId,
      string groupReservationId,
      string reservationId);
  ```

- **Service Implementation (`GroupReservationService.cs`):**
  - Inject `IOrderRepository orderRepository` into constructor (already registered in DI container).
  - Workflow in `SubmitConsolidatedPreOrderAsync`:
    1. **Fetch & Validate Group Reservation:**
       - Call `groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)`.
       - If null: throw `GroupReservationNotFoundException($"Group reservation with ID {groupReservationId} was not found.")`.
       - If `groupReservation.ReservationId != reservationId`: throw `ValidationException("Group reservation does not match the specified reservation ID.")`.
       - If `groupReservation.Status == GroupReservationStatus.Submitted`: throw `GroupReservationSubmittedException()`.
       - If `groupReservation.Status == GroupReservationStatus.Closed`: throw `GroupReservationExpiredException()`.
    2. **Fetch & Validate Reservation & Ownership:**
       - Call `reservationRepository.GetReservationByIdAsync(reservationId)`.
       - If null: throw `ReservationNotFoundException($"Reservation with ID {reservationId} was not found.")`.
       - If `!string.Equals(reservation.CustomerId, userId, StringComparison.OrdinalIgnoreCase)`: throw `ActionForbiddenException("Only the reservation owner can submit the consolidated pre-order.")`.
    3. **Fetch & Validate Order:**
       - Call `orderRepository.GetOrderByReservationIdAsync(reservationId)`.
       - If null: throw `ValidationException($"Order for reservation '{reservationId}' was not found.")` (ensures 400 status per requirement).
    4. **Fetch & Filter Guest Sessions:**
       - Call `guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservationId)`.
       - Filter sessions where `s.IsSubmitted == true`.
       - If `submittedSessions.Count == 0`: throw `ValidationException("There are no submitted guest sessions.")`.
    5. **Aggregate & Merge Dishes:**
       - Extract all dishes from submitted sessions: `submittedSessions.Where(s => s.Dishes != null).SelectMany(s => s.Dishes)`.
       - Group by `DishId` and sum `Quantity`:
         ```csharp
         var consolidatedDishes = allDishes
             .GroupBy(d => d.DishId)
             .Select(g => new ConsolidatedDishResponse(g.Key, g.Sum(d => d.Quantity)))
             .Where(d => d.Quantity > 0)
             .ToList();
         ```
       - If `consolidatedDishes.Count == 0`: throw `ValidationException("Submitted guest sessions contain no dishes.")`.
    6. **Apply to Order & Update Status:**
       - Initialize `order.Dishes ??= [];`.
       - For each item in `consolidatedDishes`, merge into `order.Dishes` (sum quantity if existing dish, or append new `OrderItem` with `State = OrderItemState.Pending`).
       - Set `order.Status = OrderStatus.SubmittedByCustomer`.
       - Call `await orderRepository.SaveOrderAsync(order)`.
    7. **Update Group Reservation Status:**
       - Set `groupReservation.Status = GroupReservationStatus.Submitted`.
       - Call `await groupReservationRepository.SaveGroupReservationAsync(groupReservation)`.
    8. **Return Result:** Return `consolidatedDishes`.

### 3.3 Handler Layer (`APIHandler.Handlers.GroupReservations`)
Create **`SubmitConsolidatedPreOrderHandler.cs`**:
- Implements `IApiRequestHandler`.
- Extracts `groupReservationId` from `request.TryGetPathValue("groupReservationId", out var groupReservationId)`. If missing or empty, returns 400 Bad Request.
- Parses request body to `SubmitConsolidatedPreOrderRequest`. If body is null or `ReservationId` is missing/empty, returns 400 Bad Request.
- Validates authorization claim: `UserContextHelper.GetAuthenticatedUserId(request)`. If missing or whitespace, returns 401 Unauthorized.
- Calls `_groupReservationService.SubmitConsolidatedPreOrderAsync(userId, groupReservationId, requestBody.ReservationId)`.
- Exception Handling:
  - `ReservationNotFoundException` -> 404
  - `GroupReservationNotFoundException` -> 404
  - `ActionForbiddenException` -> 403
  - `ValidationException` -> 400
- Returns `ResponseBuilder.CreateSuccessResponse(result)`.

### 3.4 Routing & DI (`APIHandler.Routing.RouteRegistry.cs`)
- Register the route in `GetGuestAndGroupRoutes()`:
  ```csharp
  new RouteHandler(
      new Route(HttpMethods.POST, "/group-reservations/{groupReservationId}/submit"),
      ActivatorUtilities.CreateInstance<SubmitConsolidatedPreOrderHandler>(provider))
  ```

### 3.5 Infrastructure Configuration (`deployment_resources.json`)
- Add the API Gateway endpoint resource:
  ```json
  "/group-reservations/{groupReservationId}/submit": {
    "enable_cors": {
      "state": true
    },
    "POST": {
      "authorization_type": "cognito_authorizer",
      "integration_type": "lambda",
      "lambda_name": "api-handler",
      "api_key_required": false,
      "method_request_parameters": {},
      "integration_request_body_template": {},
      "responses": [],
      "integration_responses": [],
      "default_error_pattern": true,
      "enable_proxy": true
    }
  }
  ```

---

## 4. Testing Strategy

### 4.1 Service Unit Tests (`Tests/Services/GroupReservationServiceTests.cs`)
- `SubmitConsolidatedPreOrderAsync_ValidSubmittedSessions_MergesDishesAndUpdatesStatuses`: Verifies successful aggregation, merging duplicates, updating order to `SubmittedByCustomer`, updating group reservation to `Submitted`, and saving both entities.
- `SubmitConsolidatedPreOrderAsync_GroupReservationNotFound_ThrowsGroupReservationNotFoundException` (404)
- `SubmitConsolidatedPreOrderAsync_ReservationNotFound_ThrowsReservationNotFoundException` (404)
- `SubmitConsolidatedPreOrderAsync_ReservationIdMismatch_ThrowsValidationException` (400)
- `SubmitConsolidatedPreOrderAsync_CallerNotOwner_ThrowsActionForbiddenException` (403)
- `SubmitConsolidatedPreOrderAsync_OrderNotFound_ThrowsValidationException` (400)
- `SubmitConsolidatedPreOrderAsync_NoSubmittedSessions_ThrowsValidationException` (400)
- `SubmitConsolidatedPreOrderAsync_SubmittedSessionsHaveNoDishes_ThrowsValidationException` (400)
- `SubmitConsolidatedPreOrderAsync_GroupReservationAlreadySubmitted_ThrowsGroupReservationSubmittedException` (400)

### 4.2 Handler Unit Tests (`Tests/Handlers/GroupReservations/SubmitConsolidatedPreOrderHandlerTests.cs`)
- `HandleRequest_ValidRequest_Returns200WithConsolidatedDishes`
- `HandleRequest_MissingGroupReservationId_Returns400`
- `HandleRequest_MissingOrInvalidBody_Returns400`
- `HandleRequest_MissingReservationIdInBody_Returns400`
- `HandleRequest_Unauthenticated_Returns401`
- `HandleRequest_ReservationNotFound_Returns404`
- `HandleRequest_GroupReservationNotFound_Returns404`
- `HandleRequest_ActionForbidden_Returns403`
- `HandleRequest_ValidationException_Returns400`

---

## 5. Phased Checklist

- [ ] **Phase 1: Approval Gate**
  - [ ] Secure user approval for this implementation plan.
- [ ] **Phase 2: Backend Implementation (`aws-lambda-agent`)**
  - [ ] Create `SubmitConsolidatedPreOrderRequest.cs` and `ConsolidatedDishResponse.cs` in `Models/GroupReservation/`.
  - [ ] Update `IGroupReservationService.cs` and implement `SubmitConsolidatedPreOrderAsync` in `GroupReservationService.cs`.
  - [ ] Create `SubmitConsolidatedPreOrderHandler.cs` in `Handlers/GroupReservations/`.
  - [ ] Register route in `RouteRegistry.cs`.
  - [ ] Add route configuration to `deployment_resources.json`.
  - [ ] Verify clean compilation via `dotnet-build-check`.
- [ ] **Phase 3: Unit Testing (`unit-test-agent`)**
  - [ ] Update `GroupReservationServiceTests.cs` (add tests and ensure existing tests pass with injected `IOrderRepository`).
  - [ ] Create `SubmitConsolidatedPreOrderHandlerTests.cs` covering all scenarios.
  - [ ] Execute tests via `dotnet-test-runner` and verify 100% pass rate.
- [ ] **Phase 4: Code Review Audit (`code-review-agent`)**
  - [ ] Inspect git diff, verify DynamoDB query efficiency, security, .NET 10 standards.
  - [ ] Obtain `APPROVED` verdict.
- [ ] **Phase 5: Delivery & Reporting**
  - [ ] Save delivery report in `.antigravity/docs/backend/reports/submit-consolidated-preorder-report.md`.
  - [ ] Clean up transient files and present summary to user.

