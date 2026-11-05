# AQA Homework Report: Group Pre-Order Automated Testing Suite

- **Task Specification:** [homework-aqa.md](homework-aqa.md)
- **Chosen Track:** **Track 1: Group Pre-Order** (Testing our own deployed Backend and Frontend)
- **Target Stack:** .NET 10 (C# 13), Reqnroll (Cucumber/Gherkin BDD), TUnit, Microsoft Playwright for .NET, Screenplay Pattern
- **Status:** Complete & Verified (**Level 1: Guest Sessions List**, **Level 2: Owner Session & Consolidated Submit**, **Level 3: Guest Session Removal**)
- **Quality Gates:** Build: 0 errors, 0 warnings | Discovered Tests: 39 new Track 1 scenarios (121 total) | Quality Pipeline: 100% Passed (39/39)

---

## 1. Requirements Delivery Matrix

### Level 1: Guest Sessions List

| # | Scenario Description | Layer | Persona | Primary Action / Flow | Expected Outcome & Verifications | Undefined Behavior / Status Code |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **1** | Owner views participants, dishes, quantities, and individual totals | **UI** | Owner | Owner opens `/reservations/:id/group-order` after guests joined and selected dishes. | All participants (Owner + Guests) rendered in `ul.participants-list`; each dish name, quantity (`× N`), and price itemized; individual subtotals rendered. Grand total equals sum of subtotals. | `200` OK snapshot in DOM |
| **2** | Owner retrieves sessions list via API | **API** | Owner | `GET /guest/groups/{groupId}/sessions` with Owner JWT token. | Response contains all participants with `items`, individual `total`s, and grand `total`. | `200` OK |
| **3** | Grand total dynamically updates on cart modification | **UI** | Owner, Guest | Guest adds a dish to their cart and confirms; Owner clicks `Refresh` on group pre-order page. | Grand total in `footer.footer-summary` updates dynamically and matches the newly recalculated sum of all subtotals. | Live client-side state sync |
| **4** | Service recalculates totals when guest adds dishes | **API** | Owner, Guest | Guest calls `POST /guest/sessions/{id}/dishes`; Owner calls `GET /guest/groups/{groupId}/sessions`. | Grand `total` equals the sum of updated participant subtotals. | `200` OK |
| **5** | Unconfirmed carts show choosing status, empty carts show empty indicator | **UI** | Owner | Owner opens group pre-order with unsubmitted and empty guest carts. | Unsubmitted guest displays `.chip.pending` (`Still choosing`); participant with no dishes displays `p.empty` (`No dishes yet`) and subtotal `$0.00`. | DOM indicator checks |
| **6** | Unsubmitted carts and empty dishes returned in sessions response | **API** | Owner | Owner calls `GET /guest/groups/{groupId}/sessions`. | Unconfirmed guest has `isSubmitted: false`; empty guest has `items: []` and `total: 0`. | `200` OK |
| **7** | Owner opens empty group pre-order | **UI** | Owner | Owner books group table with no guests joined; opens group pre-order page. | Displays empty pre-order banner (`Participants haven’t added any dishes yet`); grand total displays `$0.00`. | Specified empty view (`participants: []`, `total: 0`) |
| **8** | Service returns empty participants and zero total for empty group | **API** | Owner | `GET /guest/groups/{groupId}/sessions` on group with 0 guests. | 0 guest participants; `total: 0`. | `200` OK, `total: 0` |
| **9** | Another customer cannot view group sessions | **API** | Customer | Authenticated customer (not owner) calls `GET /guest/groups/{groupId}/sessions`. | Blocked by tenant isolation check. | `403` Forbidden (`"Only the reservation owner can perform this action."`) |
| **10** | Waiter cannot view customer's group pre-order sessions | **API** | Waiter | Authenticated waiter calls `GET /guest/groups/{groupId}/sessions`. | Blocked by ownership validation. | `403` Forbidden (`"Only the reservation owner can perform this action."`) |
| **11** | Unauthorized visitor cannot view sessions | **API** | Visitor | Unauthenticated request (no Bearer token) to `GET /guest/groups/{groupId}/sessions`. | Blocked by authorizer. | `401` Unauthorized |
| **12** | Unauthorized visitor redirected away from group pre-order page | **UI** | Visitor | Unauthenticated browser navigates to `/reservations/:id/group-order`. | Angular `AuthGuard` intercepts navigation and redirects to `/login`. | Client-side redirect (`**/login`) |
| **13** | Non-existent group returns 404 | **API** | Owner | `GET /guest/groups/{nonExistentId}/sessions`. | Group lookup fails in repository. | `404` Not Found |
| **14** | Opening non-existent group shows not found message | **UI** | Owner | Browser navigates to non-existent group pre-order URL. | Error alert displays `Group reservation not found`. | Client-side error state |

---

### Level 2: Owner Session & Consolidated Submit

| # | Scenario Description | Layer | Persona | Primary Action / Flow | Expected Outcome & Verifications | Undefined Behavior / Status Code |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **15** | Owner confirms consolidated pre-order with merged identical dishes | **UI** | Owner | Owner clicks `Submit Pre-Order` and confirms modal. | Status badge updates to `Sent to Restaurant`; identical dishes are merged with summed quantities; unconfirmed carts excluded; submit button hidden. | `200` OK snapshot in DOM |
| **16** | Owner confirms consolidated pre-order via service | **API** | Owner | `POST /group-reservations/{id}/submit` with `{ reservationId }` and owner token. | Returns 200 with list of consolidated dishes and summed quantities; group reservation status transitions to `Submitted`. | `200` OK |
| **17** | Waiter views confirmed consolidated pre-order | **UI** | Waiter | Waiter navigates to `/waiter-pre-order` for the confirmed reservation. | Displays exact items and consolidated quantities confirmed by Owner. | `200` OK snapshot in DOM |
| **18** | Waiter retrieves confirmed order details via service | **API** | Waiter | `GET /orders/{reservationId}` with waiter token. | Order status equals `SubmittedByCustomer`; contains exact consolidated dishes and summed quantities. | `200` OK |
| **19** | Submitting refused when no guest carts confirmed | **API** | Owner | Owner submits pre-order when all guests are still choosing (`isSubmitted: false`). | Rejected by validation check. | `400` Bad Request (`"There are no submitted guest sessions."`) |
| **20** | Owner cannot submit when no confirmed dishes exist | **UI** | Owner | Owner opens group pre-order where guests haven't submitted any dishes. | `Submit Pre-Order` button is `disabled`. | Disabled submit action in DOM |
| **21** | Submitting refused when confirmed carts contain no dishes | **API** | Owner | Owner submits pre-order when confirmed guests have empty carts. | Rejected by validation check. | `400` Bad Request (`"Submitted guest sessions contain no dishes."`) |
| **22** | Duplicate submission of confirmed group pre-order is refused | **API** | Owner | Owner submits an already submitted group pre-order a second time. | Group state guard rejects duplicate submission. | `400` Bad Request (`"Group reservation was submitted."`) |
| **23** | Submitting for non-existent group returns 404 | **API** | Owner | `POST /group-reservations/{nonExistentId}/submit`. | Group lookup fails. | `404` Not Found |
| **24** | Submitting with mismatched reservation ID is refused | **API** | Owner | `POST /group-reservations/{id}/submit` with non-matching `reservationId`. | Mismatch validation rejects request. | `400` Bad Request |
| **25** | Non-owner cannot submit consolidated pre-order | **API** | Customer | Another authenticated user calls `POST /group-reservations/{id}/submit`. | Ownership authorization rejects non-owner. | `403` Forbidden (`"Only the reservation owner can submit the consolidated pre-order."`) |
| **26** | Waiter cannot submit consolidated pre-order | **API** | Waiter | Authenticated waiter calls `POST /group-reservations/{id}/submit`. | Ownership authorization rejects waiter. | `403` Forbidden (`"Only the reservation owner can submit the consolidated pre-order."`) |
| **27** | Unauthorized visitor cannot submit pre-order | **API** | Visitor | Unauthenticated call to `POST /group-reservations/{id}/submit`. | Authorizer rejects missing token. | `401` Unauthorized |

---

### Level 3: Guest Session Removal

| # | Scenario Description | Layer | Persona | Primary Action / Flow | Expected Outcome & Verifications | Undefined Behavior / Status Code |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **28** | Owner removes a participant in the UI | **UI** | Owner | Owner clicks `Remove Guest` on participant card and confirms removal dialog. | Target participant is removed from participant list; remaining participants stay intact; grand total is immediately recalculated to match remaining participants. | `200` OK snapshot in DOM |
| **29** | Owner removes a participant via the service | **API** | Owner | `DELETE /guest/groups/{groupId}/sessions/{sessionId}` with owner token. | Returns 200 with updated participant list excluding the removed guest and recalculated total. | `200` OK |
| **30** | Owner removes a participant who already confirmed their cart | **API** | Owner | Owner removes a guest who has `isSubmitted: true`. | Service allows removal; returned participant list excludes the guest and total excludes their dishes. | `200` OK |
| **31** | Owner removes the last remaining participant in the UI | **UI** | Owner | Group has 1 guest; owner removes the guest in UI. | 0 guest participants remain; empty pre-order banner (`Participants haven’t added any dishes yet`) is displayed; grand total is `$0.00`. | DOM empty state |
| **32** | Owner removes the last remaining participant via the service | **API** | Owner | Group has 1 guest; owner deletes the session via API. | Returns 200; participants list contains only owner session (0 guests); grand total is `0`. | `200` OK |
| **33** | Removing a participant belonging to another group is refused | **API** | Owner | Owner attempts to delete guest session ID from an independent second group (`DELETE /guest/groups/{g1}/sessions/{s2}`). | Session group mismatch validation rejects request. | `400` Bad Request (`"Guest session does not belong to this group reservation."`) |
| **34** | Removing a non-existent participant returns not found | **API** | Owner | `DELETE /guest/groups/{groupId}/sessions/{nonExistentSessionId}`. | Session lookup fails. | `404` Not Found (`"Guest session with id ... not found."`) |
| **35** | Removing the owner session is refused | **API** | Owner | Owner attempts to delete their own session (`DELETE /guest/groups/{groupId}/sessions/{ownerSessionId}`). | Owner session protection rejects deletion. | `400` Bad Request (`"Cannot remove the owner session from the group order."`) |
| **36** | Another customer cannot remove a participant | **API** | Customer | Another authenticated user calls `DELETE /guest/groups/{groupId}/sessions/{sessionId}`. | Ownership authorization rejects non-owner. | `403` Forbidden (`"Only the reservation owner can perform this action."`) |
| **37** | Waiter cannot remove a participant | **API** | Waiter | Authenticated waiter calls `DELETE /guest/groups/{groupId}/sessions/{sessionId}`. | Ownership authorization rejects waiter. | `403` Forbidden (`"Only the reservation owner can perform this action."`) |
| **38** | Unauthorized visitor cannot remove a participant | **API** | Visitor | Unauthenticated call to `DELETE /guest/groups/{groupId}/sessions/{sessionId}`. | Authorizer rejects missing token. | `401` Unauthorized |
| **39** | End-to-End flow: Add dishes → Owner removes one guest → Submits → Waiter sees confirmed order without removed guest | **E2E (UI + API)** | Owner, Guests, Waiter | Multiple guests add dishes; Owner reviews list and removes guest; Owner confirms pre-order; Waiter reviews order. | Waiter sees confirmed order containing only dishes and quantities from the non-removed guest. | `200` OK end-to-end flow |

---

## 2. Test Pyramid & Architectural Approach

### Chosen Architecture: BDD + Screenplay Pattern
- **Gherkin Business Layer (`Features/`):**
  - `Features/GroupPreOrder.feature`: Level 1 (Guest Sessions List, 14 scenarios).
  - `Features/GroupPreOrderConfirmation.feature`: Level 2 (Owner Session & Consolidated Submit, 13 scenarios).
  - `Features/GroupPreOrderGuestRemoval.feature`: Level 3 (Guest Session Removal, 12 scenarios).
  Scenarios written in declarative business language focusing on user goals and observable outcomes, strictly avoiding URLs, HTTP verbs, CSS selectors, or status codes.
- **Screenplay Separation of Concerns:**
  - **Actor:** Represents the persona executing actions (`Owner`, `Guest`, `Customer`, `Waiter`, `Visitor`).
  - **Abilities:**
    - `BrowseTheWeb`: Controls Playwright browser pages and tracing.
    - `CallTheApi`: Performs direct HTTP requests using `IAPIRequestContext` (available to both API and UI actors).
    - `IRemember`: Manages isolated per-scenario state in Reqnroll's typed `ScenarioContext`.
  - **Tasks (`Screenplay/Tasks/`):**
    - `SetUpGroupReservation`: Resolves a valid restaurant branch with an assigned waiter (via standing waiter mapping or seeded branch), provisions an isolated table, registers and authenticates the owner, creates the group reservation, and retrieves the group invite token.
    - `SetUpAnotherGroupReservation`: Sets up an independent second group with its own owner, table, reservation, and guests for cross-group isolation testing while preserving the primary group context.
    - `AddGuestToGroupPreOrder`: Creates guest session and populates carts via API for fast, deterministic setup.
    - `ModifyGuestCart`: Dynamically adds/removes items from guest carts.
    - `OpenTheGroupPreOrderPage` / `RefreshTheGroupPreOrder`: Controls UI navigation and refresh actions.
    - `SubmitTheConsolidatedPreOrderInUi`: Opens the submit modal on the review page, verifies confirmation prompt, and confirms order submission.
    - `SubmitTheConsolidatedPreOrderThroughTheApi`: Submits consolidated order under various roles (`Owner`, `AnotherCustomer`, `Waiter`, `Unauthorized`).
    - `RemoveGuestFromGroupInUi`: Clicks `Remove Guest` button on the participant card, confirms modal dialog, and waits for DOM detachment.
    - `RemoveGuestThroughTheApi`: Calls `DELETE /guest/groups/{groupId}/sessions/{sessionId}` under specific roles or with explicit IDs.
    - `OpenTheWaiterPreOrderPage` / `AskForOrderByReservationId`: Enables waiter to inspect the confirmed order in UI and via API.
  - **Questions (`Screenplay/Questions/`):**
    - `TheGroupOrderParticipants`: Reads participant cards, item lists, and subtotals.
    - `TheGrandTotalShown`: Parses UI grand total.
    - `TheEmptyDishesBannerIsShown` & `TheGroupOrderErrorIsShown`: Validates UI alert and banner states.
    - `TheRestaurantOrderSummary`: Inspects consolidated restaurant order summary items and quantities.
    - `TheConsolidatedDishesResponse`: Returns typed `List<ConsolidatedDishDto>`.
    - `TheWaiterOrderResponse`: Reads `GetOrderResponse` returned by the orders API.
    - `TheGuestSessionsResponse`: Returns typed `GetAllSessionsResponse` from the API.
  - **Page Objects & Components (`Ui/`):**
    - `GroupOrderPage`: Encapsulates locators for the group pre-order page, submit and remove modals, and consolidated order summary.
    - `WaiterPreOrderPage`: Encapsulates waiter view of the pre-order and dishes list.
    - `ParticipantCardComponent`: Encapsulates participant card elements (`.chip`, `ul.items`, subtotal, remove guest button).
    - `ReservationCardComponent`: Encapsulates reservation card actions (including `Group Pre-order` navigation).
  - **API Clients (`Api/`):**
    - `GuestSessionsApi`: Manages `/guest/sessions/*` and `/guest/groups/*` (including `DeleteGuestSessionAsync`).
    - `GroupReservationsApi`: Manages `/reservations/*/invite-token`, `/guest/invites/*`, and `/group-reservations/*/submit`.
    - `OrdersApi`: Manages `/orders/*`.

---

## 3. Undefined Behavior Handling (Explicit Decisions)

Where the homework specification leaves status codes or edge behaviors unspecified, the following deterministic rules are implemented and validated:

1. **Access Control (Non-Owner & Waiter Access):**
   - **Decision:** HTTP `403 Forbidden` with body `{"error": "Only the reservation owner can perform this action."}` for sessions view and participant removal; `{"error": "Only the reservation owner can submit the consolidated pre-order."}` for order submission.
   - **Rationale:** Group pre-order management is strictly private to the reservation owner. Waiters review finalized orders via the waiter dashboard after submission.
2. **Cross-Group Removal (Mismatched Participant):**
   - **Decision:** HTTP `400 Bad Request` with message `"Guest session does not belong to this group reservation."`.
   - **Rationale:** Prevents tenant cross-contamination between different group pre-orders.
3. **Owner Session Deletion:**
   - **Decision:** HTTP `400 Bad Request` with message `"Cannot remove the owner session from the group order."`.
   - **Rationale:** The group reservation owner is the anchor of the order and cannot remove themselves.
4. **Duplicate Submission:**
   - **Decision:** HTTP `400 Bad Request` with body `{"error": "Group reservation was submitted."}`.
   - **Rationale:** Once a group pre-order is submitted, the status transitions to `Submitted` and further modifications or duplicate submissions are rejected.
5. **Submission With No Submitted Guest Carts:**
   - **Decision:** HTTP `400 Bad Request` with message `"There are no submitted guest sessions."`. In the UI, the submit button is `disabled`.
6. **Unauthorized Access (Missing Token):**
   - **Decision:** HTTP `401 Unauthorized` for API; automated redirect to `/login` via Angular `AuthGuard` for UI.
7. **Non-Existent Group Reservation / Guest Session:**
   - **Decision:** HTTP `404 Not Found` for API; UI renders `.error-container.not-found` with text `"Group reservation not found"`.
8. **Empty Group Pre-Order (No Guests Joined Yet / Last Guest Removed):**
   - **Decision:** API returns 0 guest participants and `total: 0`. UI displays `.empty-dishes-banner` with `"Participants haven’t added any dishes yet"` and grand total `$0.00`.

---

## 4. Test Execution & Tagging Strategy

All scenarios are strictly tagged, allowing targeted execution by level or feature:

### Commands to Run:
```bash
# 1. Build framework (TreatWarningsAsErrors: true)
dotnet build

# 2. Run Level 1 Group Pre-Order tests (14 scenarios)
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=level-1]"

# 3. Run Level 2 Consolidated Confirmation tests (13 scenarios)
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=level-2]"

# 4. Run Level 3 Guest Session Removal tests (12 scenarios)
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=level-3]"

# 5. Run entire Track 1 suite (Levels 1, 2, and 3 - 39 scenarios)
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=group-preorder]"

# 6. Run entire suite with TRX report
dotnet run --no-build -- --report-trx

# 7. Run UI tests with headed browser
HEADED=true dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=level-3]"
```

### Artifacts and Reports:
- **TUnit Test Report:** `TestResults/*-report.html` & `TestResults/*.trx`
- **Reqnroll BDD Report:** `TestResults/reqnroll/`
- **Playwright Trace Recordings:** `TestResults/traces/*.zip` (Inspect with `npx playwright show-trace <path-to-trace.zip>`)

---

## 5. Verification & Quality Gates

1. **Compilation Gate:** Solution compiles with **0 warnings, 0 errors** under .NET 10 (`TreatWarningsAsErrors: true`).
2. **In-Process Feature Code Generation:** Configured `<ReqnrollGenerationTaskFactory>AssemblyTaskFactory</ReqnrollGenerationTaskFactory>` in `.csproj` to ensure deterministic, in-process code-behind generation without task host spawning failures.
3. **No Tracked Junk:** All generated code-behind (`*.feature.cs`) and build artifacts are strictly excluded by `.gitignore`.
4. **Test Discovery & Execution:**
   - **Total Track 1 Scenarios:** **39** (14 Level 1 + 13 Level 2 + 12 Level 3).
   - **Execution Result:** **39 Passed, 0 Failed, 0 Skipped (100% Success)**.
   - **Total Solution Tests:** **121** tests discovered across the framework.
