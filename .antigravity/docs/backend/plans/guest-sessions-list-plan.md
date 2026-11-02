# Implementation Plan: Level 1 - Guest Sessions List API

## 1. Goal & Overview
Implement the endpoint **`GET /guest/groups/{groupReservationId}/sessions`** for the `restaurant-backend-app` service.
This endpoint enables the Reservation Owner to retrieve a consolidated snapshot of all active participant sessions (both the Owner and invited Guests) linked to a specific group reservation. The snapshot includes:
- Participant metadata: `id`, `name`, `type` (`Owner` | `Guest`), and `isSubmitted` status.
- Selected dishes per participant: dish details (`id`, `name`, `price`, etc.) and `quantity`.
- Calculated individual subtotal per participant and the overall grand total across all participants.

---

## 2. API Contract Specification

- **Endpoint Path:** `/guest/groups/{groupReservationId}/sessions`
- **HTTP Method:** `GET`
- **Authorization:** `cognito_authorizer` (Bearer JWT in `Authorization` header)
- **Path Parameters:**
  - `groupReservationId` (string, required): The unique identifier of the group reservation.
- **Request Body:** None.

### HTTP Responses:
- **`200 OK`**: Successfully retrieved participant sessions and order totals.
  ```json
  {
    "participants": [
      {
        "id": "session1",
        "type": "Guest",
        "name": "Andrii",
        "items": [
          {
            "dish": {
              "id": "dish1",
              "name": "Caesar Salad",
              "description": "Crisp romaine lettuce with Caesar dressing",
              "dishType": "Salad",
              "price": 180,
              "weight": "250g",
              "calories": "320",
              "proteins": "12",
              "fats": "20",
              "carbohydrates": "15",
              "vitamins": "A, C",
              "imageUrl": "https://...",
              "state": "Available"
            },
            "quantity": 2
          }
        ],
        "total": 360,
        "isSubmitted": true
      },
      {
        "id": "session2",
        "type": "Guest",
        "name": "Olena",
        "items": [
          {
            "dish": {
              "id": "dish2",
              "name": "Tom Yum",
              "description": "Spicy and sour Thai soup",
              "dishType": "Soup",
              "price": 220,
              "weight": "350g",
              "calories": "280",
              "proteins": "18",
              "fats": "10",
              "carbohydrates": "25",
              "vitamins": "C",
              "imageUrl": "https://...",
              "state": "Available"
            },
            "quantity": 1
          }
        ],
        "total": 220,
        "isSubmitted": false
      }
    ],
    "total": 580
  }
  ```
- **`400 Bad Request`**: Missing `groupReservationId` in path or invalid parameters.
- **`401 Unauthorized`**: Caller is not authenticated or authorizer claims (`sub`) are missing.
- **`403 Forbidden`**: Authenticated user is not the Owner of the parent reservation (`customerId != userId`).
- **`404 Not Found`**:
  - Group reservation with ID `groupReservationId` does not exist (`GroupReservationNotFoundException`).
  - Associated parent reservation does not exist (`ReservationNotFoundException`).

---

## 3. Architecture & Component Breakdown

### 3.1 DTOs and Data Models
- **Existing Models Reused:**
  - `APIHandler.Models.GuestSessions.GetAllSessionsResponse`: `(IEnumerable<Participant> Participants, decimal Total)`
  - `APIHandler.Models.GuestSessions.Participant`: `(string? Id, ParticipantType Type, string Name, IEnumerable<OrderItemResponse> Items, decimal Total, bool IsSubmitted)`
  - `APIHandler.Models.GuestSessions.ParticipantType`: `Owner`, `Guest`
  - `APIHandler.Models.Orders.OrderItemResponse`: `(DishResponse Dish, int Quantity)`
  - `APIHandler.Models.Dishes.DishResponse`: Dish details DTO

### 3.2 Service Layer (`APIHandler.Services.GuestSessions`)
- **Interface Update (`IGuestSessionService.cs`):**
  Add method:
  ```csharp
  Task<GetAllSessionsResponse> GetGuestSessionsAsync(string userId, string groupReservationId);
  ```
- **Implementation (`GuestSessionService.cs`):**
  - Verify caller authorization via `ValidateReservationOwnerAsync(groupReservationId, userId)`.
  - Fetch all guest sessions via `guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservationId)` (indexed GSI query).
  - Extract distinct `dishId` values from all sessions' dishes (`session.Dishes`).
  - Fetch all referenced dishes in batch using `dishRepository.GetDishesByIdsAsync(dishIds)` and index them by ID (`dishesById`).
  - Map each session into a `Participant`:
    - Participant `Type`: `guestSession.IsOwnerSession ? ParticipantType.Owner : ParticipantType.Guest`.
    - Participant `Name`: if `IsOwnerSession` and name is generic, retrieve owner name from Cognito or fallback to `guestSession.GuestName`.
    - Map each item to `OrderItemResponse` with `DishResponse` (via `DishMapper.ToDishResponse`) and `Quantity`.
    - Calculate participant subtotal: `items.Sum(i => i.Quantity * i.Dish.Price)`.
  - Return `GetAllSessionsResponse(participants, participants.Sum(p => p.Total))`.
  - Refactor session mapping logic into a shared private helper `BuildGroupSessionsResponseAsync` shared with `DeleteGuestSessionAsync` to adhere to DRY principles.

### 3.3 Handler Layer (`APIHandler.Handlers.GuestSessions`)
- **Create `GetGuestSessionsHandler.cs`:**
  - Implements `IApiRequestHandler`.
  - Extracts `groupReservationId` from path parameters (`request.TryGetPathValue("groupReservationId", out var groupReservationId)`).
  - Extracts `userId` from authorizer claims (`request.RequestContext?.Authorizer?.Claims["sub"]`).
  - Calls `_guestSessionService.GetGuestSessionsAsync(userId, groupReservationId)`.
  - Exception handling:
    - `GroupReservationNotFoundException` -> 404
    - `ReservationNotFoundException` -> 404
    - `ActionForbiddenException` -> 403
    - `ValidationException` -> 400
  - Returns `ResponseBuilder.CreateSuccessResponse(result)`.

### 3.4 Routing & DI Registration (`APIHandler.Routing.RouteRegistry.cs`)
- Register the route in `GetGuestAndGroupRoutes()`:
  ```csharp
  new RouteHandler(
      new Route(HttpMethods.GET, "/guest/groups/{groupReservationId}/sessions"),
      ActivatorUtilities.CreateInstance<GetGuestSessionsHandler>(provider)
  )
  ```

### 3.5 Infrastructure as Code (`deployment_resources.json`)
- Add the API Gateway resource definition in `backend/restaurant-backend-app/deployment_resources.json` under `restaurant-api`:
  ```json
  "/guest/groups/{groupReservationId}/sessions": {
    "enable_cors": {
      "state": true
    },
    "GET": {
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

## 4. Testing Strategy (QA)

### 4.1 Unit Tests in `GuestSessionServiceTests.cs`
- `GetGuestSessionsAsync_ValidOwnerAndSessionsExist_ReturnsParticipantsAndTotal`:
  - Arrange: group reservation, parent reservation owned by caller (`CustomerId == userId`), multiple guest sessions with dishes.
  - Act: call `GetGuestSessionsAsync(userId, groupReservationId)`.
  - Assert: returns correct participant list, item details, individual totals, and grand total.
- `GetGuestSessionsAsync_EmptyDishes_ReturnsZeroTotalAndEmptyItems`:
  - Verify participant with 0 dishes produces total 0.
- `GetGuestSessionsAsync_GroupReservationNotFound_ThrowsGroupReservationNotFoundException` (404 scenario).
- `GetGuestSessionsAsync_ParentReservationNotFound_ThrowsReservationNotFoundException` (404 scenario).
- `GetGuestSessionsAsync_CallerNotOwner_ThrowsActionForbiddenException` (403 scenario).

### 4.2 Unit Tests in `GetGuestSessionsHandlerTests.cs`
- `HandleRequest_ValidRequest_Returns200WithSessionsPayload`:
  - Mock service returning `GetAllSessionsResponse`, verify status code 200 and response body.
- `HandleRequest_MissingGroupReservationId_Returns400`:
  - Request without `groupReservationId` path parameter returns 400.
- `HandleRequest_Unauthenticated_Returns401`:
  - Request with null authorizer or missing `sub` claim returns 401.
- `HandleRequest_ForbiddenException_Returns403`:
  - Service throws `ActionForbiddenException`, handler returns 403.
- `HandleRequest_GroupNotFound_Returns404`:
  - Service throws `GroupReservationNotFoundException`, handler returns 404.

### 4.3 Build & Test Verification Gates
- Execute `dotnet build backend/restaurant-backend-app/restaurant-backend-app.sln` to ensure 0 compiler errors.
- Execute `dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj` to ensure 100% test pass rate.

---

## 5. Phased Execution Checklist

- [x] **Phase 1: Intake, Plan Generation & User Approval Gate**
  - [x] Analyze requirements and existing codebase
  - [x] Author comprehensive plan at `.antigravity/docs/backend/plans/guest-sessions-list-plan.md`
  - [x] Await user explicit approval (Approved)
- [x] **Phase 2: Implementation (`aws-lambda-agent`)**
  - [x] Update `IGuestSessionService` and `GuestSessionService`
  - [x] Create `GetGuestSessionsHandler.cs`
  - [x] Register endpoint in `RouteRegistry.cs`
  - [x] Update `deployment_resources.json`
  - [x] Verify clean compilation (`dotnet build`)
- [x] **Phase 3: Unit Testing (`unit-test-agent`)**
  - [x] Add service tests to `GuestSessionServiceTests.cs`
  - [x] Create handler tests in `GetGuestSessionsHandlerTests.cs`
  - [x] Verify 100% passing tests via `dotnet test` (158 / 158 passed)
- [x] **Phase 4: Code Review & Quality Audit (`code-review-agent`)**
  - [x] Audit changes (`git diff`) for DynamoDB efficiency, async safety, tenant isolation
  - [x] Obtain `APPROVED` verdict
  - [x] Apply code polish (JSON property names, duplicate handling, route cleanup)
- [x] **Phase 5: Delivery Report & Handoff (`orchestrator-agent`)**
  - [x] Author final delivery report at `.antigravity/docs/backend/reports/guest-sessions-list-report.md`
  - [x] Present completion summary to user

