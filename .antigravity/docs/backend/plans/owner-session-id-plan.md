# Implementation Plan: Level 2 (Part 1) - Get Owner Session ID API

## 1. Goal & Overview
Implement the endpoint **`GET /reservations/{reservationId}/guest-session-id`** for the `restaurant-backend-app` service.
This endpoint enables the Reservation Owner to obtain their personal `guestSessionId` tied to the group reservation. This session ID allows the owner to participate in group ordering (adding and removing dishes in their personal cart, viewing their items, and submitting their cart) alongside invited guests.

If an owner session does not exist yet for the group reservation, the endpoint lazily initializes it in DynamoDB and returns its identifier.

---

## 2. API Contract Specification

- **Endpoint Path:** `/reservations/{reservationId}/guest-session-id`
- **HTTP Method:** `GET`
- **Authorization:** `cognito_authorizer` (Bearer JWT in `Authorization` header)
- **Path Parameters:**
  - `reservationId` (string, required): The unique identifier of the reservation.
- **Request Body:** None.

### HTTP Responses:
- **`200 OK`**: Successfully retrieved or created the owner's guest session.
  ```json
  {
    "guestSessionId": "owner-session-123"
  }
  ```
- **`400 Bad Request`**: Missing `reservationId` in path parameters.
- **`401 Unauthorized`**: Caller is not authenticated or authorizer claims (`sub`) are missing.
- **`403 Forbidden`**: Authenticated caller is not the owner of the parent reservation (`reservation.CustomerId != userId`).
- **`404 Not Found`**:
  - Reservation with ID `reservationId` does not exist (`ReservationNotFoundException`).
  - Associated group reservation does not exist (`GroupReservationNotFoundException`).

---

## 3. Architecture & Component Breakdown

### 3.1 DTOs and Data Models (`APIHandler.Models.GuestSessions`)
- **Create `GetOwnerSessionIdResponse.cs`:**
  ```csharp
  using System.Text.Json.Serialization;

  namespace APIHandler.Models.GuestSessions;

  public record GetOwnerSessionIdResponse(
      [property: JsonPropertyName("guestSessionId")] string GuestSessionId);
  ```

### 3.2 Service Layer (`APIHandler.Services.GuestSessions`)
- **Interface Update (`IGuestSessionService.cs`):**
  Add method:
  ```csharp
  Task<GetOwnerSessionIdResponse> GetOwnerSessionIdAsync(string userId, string reservationId);
  ```
- **Implementation (`GuestSessionService.cs`):**
  - Retrieve the parent reservation: `await reservationRepository.GetReservationByIdAsync(reservationId)`.
    - If null, throw `ReservationNotFoundException($"Reservation with id {reservationId} not found.")`.
  - Validate ownership:
    - If `reservation.CustomerId != userId`, throw `ActionForbiddenException("Only the reservation owner can perform this action.")`.
  - Retrieve the group reservation: `await groupReservationRepository.GetByReservationIdAsync(reservationId)`.
    - If null, throw `GroupReservationNotFoundException($"Group reservation for reservation {reservationId} not found.")`.
  - Query all guest sessions for this group: `await guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId)`.
  - Find existing owner session: `sessions.FirstOrDefault(s => s.IsOwnerSession)`.
  - If no owner session exists:
    - Create a new `GuestSession`:
      ```csharp
      ownerSession = new GuestSession
      {
          Id = Guid.NewGuid().ToString(),
          GroupReservationId = groupReservation.GroupReservationId,
          GuestName = "Owner",
          Dishes = [],
          IsOwnerSession = true,
          OwnerUserId = userId,
          IsSubmitted = false
      };
      await guestSessionRepository.SaveAsync(ownerSession);
      ```
  - Return `new GetOwnerSessionIdResponse(ownerSession.Id)`.

### 3.3 Handler Layer (`APIHandler.Handlers.GuestSessions`)
- **Create `GetOwnerSessionIdHandler.cs`:**
  - Implements `IApiRequestHandler`.
  - Extracts `reservationId` from path parameters (`request.TryGetPathValue("reservationId", out var reservationId)`).
  - Validates authentication: extracts `userId` from `request.RequestContext?.Authorizer?.Claims["sub"]`.
    - Returns `401 Unauthorized` if missing or blank.
  - Calls `_guestSessionService.GetOwnerSessionIdAsync(userId, reservationId)`.
  - Error mapping:
    - `ReservationNotFoundException` -> 404
    - `GroupReservationNotFoundException` -> 404
    - `ActionForbiddenException` -> 403
    - `ValidationException` -> 400
  - Returns `ResponseBuilder.CreateSuccessResponse(result)`.

### 3.4 Routing & DI Registration (`APIHandler.Routing.RouteRegistry.cs`)
- Register route in `GetGuestAndGroupRoutes()`:
  ```csharp
  new RouteHandler(
      new Route(HttpMethods.GET, "/reservations/{reservationId}/guest-session-id"),
      ActivatorUtilities.CreateInstance<GetOwnerSessionIdHandler>(provider)
  )
  ```

### 3.5 Infrastructure as Code (`deployment_resources.json`)
- Add API Gateway resource definition in `backend/restaurant-backend-app/deployment_resources.json` under `restaurant-api`:
  ```json
  "/reservations/{reservationId}/guest-session-id": {
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
- `GetOwnerSessionIdAsync_ExistingOwnerSession_ReturnsExistingSessionId`:
  - Arrange: Reservation with `CustomerId == userId`, GroupReservation, sessions list containing an existing `GuestSession` with `IsOwnerSession = true`.
  - Act: Call `GetOwnerSessionIdAsync(userId, reservationId)`.
  - Assert: Returns existing session ID, does not call `SaveAsync`.
- `GetOwnerSessionIdAsync_NoOwnerSessionYet_CreatesAndReturnsNewSessionId`:
  - Arrange: Reservation with `CustomerId == userId`, GroupReservation, sessions list without an owner session.
  - Act: Call `GetOwnerSessionIdAsync(userId, reservationId)`.
  - Assert: Calls `guestSessionRepository.SaveAsync` with `IsOwnerSession = true`, returns newly created ID.
- `GetOwnerSessionIdAsync_ReservationNotFound_ThrowsReservationNotFoundException` (404 scenario).
- `GetOwnerSessionIdAsync_GroupReservationNotFound_ThrowsGroupReservationNotFoundException` (404 scenario).
- `GetOwnerSessionIdAsync_CallerNotOwner_ThrowsActionForbiddenException` (403 scenario).

### 4.2 Unit Tests in `GetOwnerSessionIdHandlerTests.cs`
- `HandleRequest_ValidRequest_Returns200WithGuestSessionId`:
  - Mock service returns `GetOwnerSessionIdResponse("owner-session-123")`. Verify 200 OK and response body JSON.
- `HandleRequest_MissingReservationId_Returns400`:
  - Missing path parameter returns 400 Bad Request.
- `HandleRequest_Unauthenticated_Returns401`:
  - Missing or null claims returns 401 Unauthorized.
- `HandleRequest_ReservationNotFound_Returns404`:
  - Throws `ReservationNotFoundException`, returns 404.
- `HandleRequest_GroupReservationNotFound_Returns404`:
  - Throws `GroupReservationNotFoundException`, returns 404.
- `HandleRequest_CallerNotOwner_Returns403`:
  - Throws `ActionForbiddenException`, returns 403.

### 4.3 Build & Quality Gates
- Execute `dotnet build backend/restaurant-backend-app/restaurant-backend-app.sln` to guarantee 0 compiler errors.
- Execute `dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj` to guarantee 100% passing tests.

---

## 5. Phased Execution Checklist

- [x] **Phase 1: Intake, Plan Generation & User Approval Gate**
  - [x] Analyze requirements and codebase
  - [x] Author comprehensive plan at [owner-session-id-plan.md](owner-session-id-plan.md)
  - [x] Await user explicit approval (Approved)
- [x] **Phase 2: Implementation (`aws-lambda-agent`)**
  - [x] Create `GetOwnerSessionIdResponse.cs` in `Models/GuestSessions/`
  - [x] Update `IGuestSessionService.cs` and `GuestSessionService.cs`
  - [x] Create `GetOwnerSessionIdHandler.cs` in `Handlers/GuestSessions/`
  - [x] Register route in `RouteRegistry.cs`
  - [x] Update `deployment_resources.json`
  - [x] Verify clean compilation (`dotnet build`)
- [x] **Phase 3: Unit Testing (`unit-test-agent`)**
  - [x] Add unit tests in `GuestSessionServiceTests.cs`
  - [x] Create unit tests in `GetOwnerSessionIdHandlerTests.cs`
  - [x] Verify 100% passing tests via `dotnet test` (17 / 17 passed)
- [x] **Phase 4: Code Review & Quality Audit (`code-review-agent`)**
  - [x] Audit changes (`git diff`) against performance, architecture, and security criteria
  - [x] Obtain `APPROVED` verdict
- [x] **Phase 5: Delivery Report & Handoff (`orchestrator-agent`)**
  - [x] Author final delivery report at `.antigravity/docs/backend/reports/owner-session-id-report.md`
  - [x] Present completion summary to user

