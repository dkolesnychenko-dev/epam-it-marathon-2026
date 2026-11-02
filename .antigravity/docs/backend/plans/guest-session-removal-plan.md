# Implementation Plan: Level 3 - Guest Session Removal API

## 1. Goal & Overview
Implement and verify the endpoint **`DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}`** for the `restaurant-backend-app` service.

This endpoint allows the Reservation Owner to remove a specific participant's guest session from an active group reservation order. 
Upon deletion:
- The target guest session is deleted from the DynamoDB `GuestSession` repository.
- An updated group snapshot (`GetAllSessionsResponse`) containing the remaining participants, their order items, and recalculated subtotals/grand total is returned to the client.

---

## 2. API Contract Specification

- **Endpoint Path:** `/guest/groups/{groupReservationId}/sessions/{guestSessionId}`
- **HTTP Method:** `DELETE`
- **Authorization:** `cognito_authorizer` (Bearer JWT in `Authorization` header)
- **Path Parameters:**
  - `groupReservationId` (string, required): The ID of the group reservation.
  - `guestSessionId` (string, required): The ID of the participant's session to be removed.
- **Request Body:** None.

### HTTP Responses:
- **`200 OK`**: Successfully removed the guest session. Returns the updated group snapshot:
  ```json
  {
    "participants": [
      {
        "id": "session2",
        "type": "Guest",
        "name": "Olena",
        "items": [
          {
            "dish": {
              "id": "dish2",
              "name": "Tom Yum",
              "price": 220
            },
            "quantity": 1
          }
        ],
        "total": 220,
        "isSubmitted": false
      }
    ],
    "total": 220
  }
  ```
- **`400 Bad Request`**:
  - Missing `groupReservationId` or `guestSessionId` in path parameters.
  - The guest session does not belong to the specified group reservation (`ValidationException`).
  - Attempting to delete the Owner's session (`ValidationException`).
- **`401 Unauthorized`**: Caller is unauthenticated or JWT claims (`sub`) are missing/empty.
- **`403 Forbidden`**: Caller is not the Owner of the parent reservation (`customerId != userId`).
- **`404 Not Found`**:
  - `groupReservationId` does not exist (`GroupReservationNotFoundException`).
  - Associated parent reservation does not exist (`ReservationNotFoundException`).
  - `guestSessionId` does not exist (`GuestSessionNotFoundException`).

---

## 3. Architecture & Component Breakdown

### 3.1 DTOs & Models
- Reusing existing response models in `Models/GuestSessions/GetAllResponse.cs`:
  - `GetAllSessionsResponse` (`participants`: `IEnumerable<Participant>`, `total`: `decimal`)
  - `Participant` (`id`: string, `type`: `ParticipantType`, `name`: string, `items`: `IEnumerable<OrderItemResponse>`, `total`: decimal, `isSubmitted`: bool)
  - `ParticipantType`: `Owner`, `Guest`

### 3.2 Service Layer (`Services/GuestSessions/`)
- **Interface:** [`IGuestSessionService.cs`](../../../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/Services/GuestSessions/IGuestSessionService.cs)
  - Method: `Task<GetAllSessionsResponse> DeleteGuestSessionAsync(string userId, string groupReservationId, string guestSessionId);`
- **Implementation:** [`GuestSessionService.cs`](../../../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/Services/GuestSessions/GuestSessionService.cs)
  - Validation steps in `DeleteGuestSessionAsync`:
    1. Call `ValidateReservationOwnerAsync(groupReservationId, userId)`:
       - Ensures group reservation exists (or throws `GroupReservationNotFoundException` -> 404).
       - Ensures parent reservation exists (or throws `ReservationNotFoundException` -> 404).
       - Validates `reservation.CustomerId == userId` (or throws `ActionForbiddenException` -> 403).
    2. Retrieve target session via `guestSessionRepository.GetByGuestSessionId(guestSessionId)`:
       - Throws `GuestSessionNotFoundException` if null -> 404.
    3. Check `session.GroupReservationId == groupReservationId`:
       - Throws `ValidationException("Guest session does not belong to this group reservation.")` if mismatched -> 400.
    4. Guard check: Ensure target session is not an Owner session (`if (session.IsOwnerSession) throw new ValidationException("Cannot remove the owner session from the group order.");`) -> 400.
    5. Delete session: `await guestSessionRepository.DeleteAsync(session)`.
    6. Rebuild snapshot: `return await BuildGroupSessionsResponseAsync(groupReservationId, userId);`.

### 3.3 Handler Layer (`Handlers/GuestSessions/`)
- **File:** [`DeleteGuestSessionHandler.cs`](../../../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/Handlers/GuestSessions/DeleteGuestSessionHandler.cs)
  - Implements `IApiRequestHandler`.
  - Extracts path parameters `groupReservationId` and `guestSessionId` via `request.TryGetPathValue`. Returns 400 if missing or empty.
  - Extracts `sub` claim from `request.RequestContext.Authorizer.Claims`. Returns 401 if null or `string.IsNullOrWhiteSpace(userId)`.
  - Executes `_guestSessionService.DeleteGuestSessionAsync(userId, groupReservationId, guestSessionId)`.
  - Returns `ResponseBuilder.CreateSuccessResponse(updatedSnapshot)`.
  - Exception mappings:
    - `GroupReservationNotFoundException` -> 404
    - `ReservationNotFoundException` -> 404
    - `GuestSessionNotFoundException` -> 404
    - `ActionForbiddenException` -> 403
    - `ValidationException` -> 400 (`ResponseBuilder.CreateErrorCodeResponse(ex)`)

### 3.4 Routing & Documentation
- **Routing:** Verified in [`RouteRegistry.cs`](../../../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/Routing/RouteRegistry.cs):
  - Route: `DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}` bound to `DeleteGuestSessionHandler`.
- **API Documentation:** Update [`ApiRoutes.md`](../../../../backend/restaurant-backend-app/docs/ApiRoutes.md) lines 1216-1222 to document the updated group snapshot JSON response.

---

## 4. Testing Strategy

### 4.1 Unit Tests for `DeleteGuestSessionHandler`
Location: `backend/restaurant-backend-app/Tests/Handlers/GuestSessions/DeleteGuestSessionHandlerTests.cs`
- `Constructor_NullGuestSessionService_ThrowsArgumentNullException`
- `HandleRequest_ValidRequest_Returns200WithUpdatedSnapshot`
- `HandleRequest_MissingGroupReservationId_Returns400`
- `HandleRequest_MissingGuestSessionId_Returns400`
- `HandleRequest_Unauthenticated_Returns401` (missing authorizer, missing `sub` claim, empty/whitespace `sub`)
- `HandleRequest_ActionForbiddenException_Returns403`
- `HandleRequest_GroupReservationNotFoundException_Returns404`
- `HandleRequest_ReservationNotFoundException_Returns404`
- `HandleRequest_GuestSessionNotFoundException_Returns404`
- `HandleRequest_ValidationException_Returns400`

### 4.2 Unit Tests for `GuestSessionService`
Location: `backend/restaurant-backend-app/Tests/Services/GuestSessionServiceTests.cs`
- `DeleteGuestSessionAsync_ValidGuestSession_DeletesAndReturnsUpdatedSnapshot` (happy path)
- `DeleteGuestSessionAsync_CallerNotOwner_ThrowsActionForbiddenException` (403)
- `DeleteGuestSessionAsync_GroupReservationNotFound_ThrowsGroupReservationNotFoundException` (404)
- `DeleteGuestSessionAsync_ReservationNotFound_ThrowsReservationNotFoundException` (404)
- `DeleteGuestSessionAsync_GuestSessionNotFound_ThrowsGuestSessionNotFoundException` (404)
- `DeleteGuestSessionAsync_SessionBelongsToDifferentGroup_ThrowsValidationException` (400)
- `DeleteGuestSessionAsync_TargetSessionIsOwner_ThrowsValidationException` (400)

---

## 5. Phased Delivery Checklist

- [x] **Phase 1: Intake & User Approval Gate**
  - [x] Analyze requirements and existing implementation in `api-handler`.
  - [x] Author comprehensive plan at `.antigravity/docs/backend/plans/guest-session-removal-plan.md`.
  - [x] Obtain explicit user approval before delegating implementation.

- [x] **Phase 2: Implementation (`aws-lambda-agent`)**
  - [x] Update `DeleteGuestSessionHandler.cs` (add whitespace check for `userId`).
  - [x] Update `GuestSessionService.cs` (add guard check against owner session deletion).
  - [x] Update `backend/restaurant-backend-app/docs/ApiRoutes.md` response documentation.
  - [x] Validate compilation using `dotnet-build-check` (0 errors).

- [x] **Phase 3: Unit Testing (`unit-test-agent`)**
  - [x] Author `DeleteGuestSessionHandlerTests.cs` in `Tests/Handlers/GuestSessions/`.
  - [x] Add `DeleteGuestSessionAsync` test suite to `Tests/Services/GuestSessionServiceTests.cs`.
  - [x] Execute tests via `dotnet-test-runner` with `BypassSandbox: true` (100% pass gate, 22/22 passing).

- [x] **Phase 4: Code Review (`code-review-agent`)**
  - [x] Audit changes using `backend-code-review-audit` against `git diff`.
  - [x] Verify DynamoDB, async safety, and architectural standards.
  - [x] Obtain `APPROVED` verdict.

- [x] **Phase 5: Delivery & Reporting (`orchestrator-agent`)**
  - [x] Generate persistent delivery report at `.antigravity/docs/backend/reports/guest-session-removal-report.md`.
  - [x] Clean up temporary artifacts.
  - [x] Present executive summary to the user.

