# AQA Homework Report: Group Pre-Order Automated Testing Suite

- **Task Specification:** [homework-aqa.md](homework-aqa.md)
- **Chosen Track:** **Track 1: Group Pre-Order** (Testing our own deployed Backend and Frontend)
- **Target Stack:** .NET 10 (C# 13), Reqnroll (Cucumber/Gherkin BDD), TUnit, Microsoft Playwright for .NET, Screenplay Pattern
- **Status:** Complete & Verified (**Level 1: Guest Sessions List**)
- **Quality Gates:** Build: 0 errors, 0 warnings | Discovered Tests: 14 new L1 scenarios (96 total) | Quality Pipeline: Passed

---

## 1. Requirements Delivery Matrix (Level 1: Guest Sessions List)

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

## 2. Test Pyramid & Architectural Approach

### Chosen Architecture: BDD + Screenplay Pattern
- **Gherkin Business Layer (`Features/GroupPreOrder.feature`):** Scenarios written in declarative business language focusing on user goals and observable outcomes, strictly avoiding URLs, HTTP verbs, CSS selectors, or status codes.
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
    - `AskForGuestSessions`: Issues API requests under specific user personas.
  - **Questions (`Screenplay/Questions/`):**
    - `TheGroupOrderParticipants`: Reads participant cards, item lists, and subtotals.
    - `TheGrandTotalShown`: Parses UI grand total.
    - `TheEmptyDishesBannerIsShown` & `TheGroupOrderErrorIsShown`: Validates UI alert and banner states.
    - `TheGuestSessionsResponse`: Returns typed DTO responses for assertion.
  - **Page Objects & Components (`Ui/`):**
    - `GroupOrderPage`: Encapsulates locators for the group pre-order page.
    - `ParticipantCardComponent`: Encapsulates participant card elements (`.chip`, `ul.items`, subtotal).
    - `ReservationCardComponent`: Encapsulates reservation card actions (including `Group Pre-order` navigation).
  - **API Clients (`Api/`):**
    - `GuestSessionsApi`: Manages `/guest/sessions/*` and `/guest/groups/*`.
    - `GroupReservationsApi`: Manages `/reservations/*/invite-token` and `/guest/invites/*`.

---

## 3. Undefined Behavior Handling (Explicit Decisions)

Where the homework specification leaves status codes or edge behaviors unspecified, the following deterministic rules are implemented and validated:

1. **Access Control (Non-Owner & Waiter Access):**
   - **Decision:** HTTP `403 Forbidden` with body `{"error": "Only the reservation owner can perform this action."}`.
   - **Rationale:** The group pre-order review is private to the reservation owner. Waiters review finalized orders via the waiter dashboard after submission.
2. **Unauthorized Access (Missing Token):**
   - **Decision:** HTTP `401 Unauthorized` for API; automated redirect to `/login` via Angular `AuthGuard` for UI.
3. **Non-Existent Group Reservation:**
   - **Decision:** HTTP `404 Not Found` for API; UI renders `.error-container.not-found` with text `"Group reservation not found"`.
4. **Empty Group Pre-Order (No Guests Joined Yet):**
   - **Decision:** API returns 0 guest participants and `total: 0`. UI displays `.empty-dishes-banner` with `"Participants haven’t added any dishes yet"` and grand total `$0.00`.

---

## 4. Test Execution & Tagging Strategy

All Level 1 scenarios are tagged with `@level-1` and `@group-preorder`, allowing targeted execution:

### Commands to Run:
```bash
# 1. Build framework
dotnet build

# 2. Run only Level 1 Group Pre-Order tests
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=level-1]"

# 3. Run all Group Pre-Order tests (Levels 1-3)
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=group-preorder]"

# 4. Run entire suite with TRX report
dotnet run --no-build -- --report-trx

# 5. Run UI tests with headed browser
HEADED=true dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=level-1]"
```

### Artifacts and Reports:
- **TUnit Test Report:** `TestResults/*-report.html` & `TestResults/*.trx`
- **Reqnroll BDD Report:** `TestResults/reqnroll/`
- **Playwright Trace Recordings:** `TestResults/traces/*.zip` (Inspect with `npx playwright show-trace <path-to-trace.zip>`)

---

## 5. Environment & CI Configuration Clarification

### `AQA_ADMIN_USERNAME` vs `ADMIN_USERNAME` Resolution
- **Investigation:** In the base suite, 5 tests were skipped: 4 from `Reservations.feature` (`@wip`), and 1 from `Reports.feature` (`@requires-admin`).
- **Resolution:**
  1. `Settings.cs` was enhanced with automatic fallback between raw keys (`ADMIN_USERNAME`, `WAITER_USERNAME`, etc.) and `AQA_` prefixed keys (`AQA_ADMIN_USERNAME`, `AQA_WAITER_USERNAME`), preventing configuration mismatches between `.env` and environment variables.
  2. In `.github/workflows/aqa-tests.yml`, credential injection was updated to `${{ secrets.AQA_ADMIN_USERNAME || vars.AQA_ADMIN_USERNAME }}` to seamlessly support credentials stored in either GitHub Secrets or GitHub Variables.

---

## 6. Verification & Quality Gates

1. **Compilation Gate:** Solution compiles with **0 warnings, 0 errors** under .NET 10 (`TreatWarningsAsErrors: true`).
2. **In-Process Feature Code Generation:** Configured `<ReqnrollGenerationTaskFactory>AssemblyTaskFactory</ReqnrollGenerationTaskFactory>` in `.csproj` to ensure deterministic, in-process code-behind generation without task host spawning failures.
3. **No Tracked Junk:** All generated code-behind (`*.feature.cs`) and build artifacts are strictly excluded by `.gitignore`.
4. **Test Discovery:** 96 total tests discovered (82 baseline + 14 new Level 1 scenarios).

