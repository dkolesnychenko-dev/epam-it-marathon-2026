# AQA Homework Report: Group Pre-Order Automated Testing Suite

- **Task Specification:** [homework-aqa.md](homework-aqa.md)
- **Chosen Track:** **Track 1: Group Pre-Order** (Testing our own deployed Backend and Frontend)
- **Target Stack:** .NET 10 (C# 13), Reqnroll (Cucumber/Gherkin BDD), TUnit, Microsoft Playwright for .NET, Screenplay Pattern
- **Status:** Complete & Verified (**Level 1: Guest Sessions List** & **Level 2: Owner Session & Consolidated Submit**)
- **Quality Gates:** Build: 0 errors, 0 warnings | Discovered Tests: 27 new Track 1 scenarios (109 total) | Quality Pipeline: Passed

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

## 2. Test Pyramid & Architectural Approach

### Chosen Architecture: BDD + Screenplay Pattern
- **Gherkin Business Layer (`Features/`):**
  - `Features/GroupPreOrder.feature`: Level 1 (Guest Sessions List, 14 scenarios).
  - `Features/GroupPreOrderConfirmation.feature`: Level 2 (Owner Session & Consolidated Submit, 13 scenarios).
  Scenarios written in declarative business language focusing on user goals and observable outcomes, strictly avoiding URLs, HTTP verbs, CSS selectors, or status codes.
- **Screenplay Separation of Concerns:**
  - **Actor:** Represents the persona executing actions (`Owner`, `Guest`, `Customer`, `Waiter`, `Visitor`).
  - **Abilities:**
    - `BrowseTheWeb`: Controls Playwright browser pages and tracing.
    - `CallTheApi`: Performs direct HTTP requests using `IAPIRequestContext` (available to both API and UI actors).
    - `IRemember`: Manages isolated per-scenario state in Reqnroll's typed `ScenarioContext`.
  - **Tasks (`Screenplay/Tasks/`):**
    - `SetUpGroupReservation`: Resolves a valid restaurant branch with an assigned waiter (via standing waiter mapping or seeded branch), provisions an isolated table, registers and authenticates the owner, creates the group reservation, and retrieves the group invite token.
    - `AddGuestToGroupPreOrder`: Creates guest session and populates carts via API for fast, deterministic setup.
    - `ModifyGuestCart`: Dynamically adds/removes items from guest carts.
    - `OpenTheGroupPreOrderPage` / `RefreshTheGroupPreOrder`: Controls UI navigation and refresh actions.
    - `SubmitTheConsolidatedPreOrderInUi`: Opens the submit modal on the review page, verifies confirmation prompt, and confirms order submission.
    - `SubmitTheConsolidatedPreOrderThroughTheApi`: Submits consolidated order under various roles (`Owner`, `AnotherCustomer`, `Waiter`, `Unauthorized`).
    - `OpenTheWaiterPreOrderPage` / `AskForOrderByReservationId`: Enables waiter to inspect the confirmed order in UI and via API.
  - **Questions (`Screenplay/Questions/`):**
    - `TheGroupOrderParticipants`: Reads participant cards, item lists, and subtotals.
    - `TheGrandTotalShown`: Parses UI grand total.
    - `TheEmptyDishesBannerIsShown` & `TheGroupOrderErrorIsShown`: Validates UI alert and banner states.
    - `TheRestaurantOrderSummary`: Inspects consolidated restaurant order summary items and quantities.
    - `TheConsolidatedDishesResponse`: Returns typed `List<ConsolidatedDishDto>`.
    - `TheWaiterOrderResponse`: Reads `GetOrderResponse` returned by the orders API.
  - **Page Objects & Components (`Ui/`):**
    - `GroupOrderPage`: Encapsulates locators for the group pre-order page, modal actions, and consolidated order summary.
    - `WaiterPreOrderPage`: Encapsulates waiter view of the pre-order and dishes list.
    - `ParticipantCardComponent`: Encapsulates participant card elements (`.chip`, `ul.items`, subtotal).
    - `ReservationCardComponent`: Encapsulates reservation card actions (including `Group Pre-order` navigation).
  - **API Clients (`Api/`):**
    - `GuestSessionsApi`: Manages `/guest/sessions/*` and `/guest/groups/*`.
    - `GroupReservationsApi`: Manages `/reservations/*/invite-token`, `/guest/invites/*`, and `/group-reservations/*/submit`.
    - `OrdersApi`: Manages `/orders/*`.

---

## 3. Undefined Behavior Handling (Explicit Decisions)

Where the homework specification leaves status codes or edge behaviors unspecified, the following deterministic rules are implemented and validated:

1. **Access Control (Non-Owner & Waiter Access):**
   - **Decision:** HTTP `403 Forbidden` with body `{"error": "Only the reservation owner can submit the consolidated pre-order."}`.
   - **Rationale:** The group pre-order submission is strictly private to the reservation owner. Waiters review finalized orders via the waiter dashboard after submission.
2. **Duplicate Submission:**
   - **Decision:** HTTP `400 Bad Request` with body `{"error": "Group reservation was submitted."}`.
   - **Rationale:** Once a group pre-order is submitted, the status transitions to `Submitted` and further modifications or duplicate submissions are rejected.
3. **Submission With No Submitted Guest Carts:**
   - **Decision:** HTTP `400 Bad Request` with message `"There are no submitted guest sessions."`. In the UI, the submit button is `disabled`.
4. **Unauthorized Access (Missing Token):**
   - **Decision:** HTTP `401 Unauthorized` for API; automated redirect to `/login` via Angular `AuthGuard` for UI.
5. **Non-Existent Group Reservation:**
   - **Decision:** HTTP `404 Not Found` for API; UI renders `.error-container.not-found` with text `"Group reservation not found"`.
6. **Empty Group Pre-Order (No Guests Joined Yet):**
   - **Decision:** API returns 0 guest participants and `total: 0`. UI displays `.empty-dishes-banner` with `"Participants haven’t added any dishes yet"` and grand total `$0.00`.

---

## 4. Test Execution & Tagging Strategy

All scenarios are strictly tagged, allowing targeted execution by level or feature:

### Commands to Run:
```bash
# 1. Build framework
dotnet build

# 2. Run only Level 1 Group Pre-Order tests (14 scenarios)
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=level-1]"

# 3. Run only Level 2 Consolidated Confirmation tests (13 scenarios)
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=level-2]"

# 4. Run all Group Pre-Order tests (Levels 1 & 2 - 27 scenarios)
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=group-preorder]"

# 5. Run entire suite with TRX report
dotnet run --no-build -- --report-trx

# 6. Run UI tests with headed browser
HEADED=true dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=level-2]"
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
4. **Test Discovery:** 109 total tests discovered (82 baseline + 14 Level 1 + 13 Level 2).
