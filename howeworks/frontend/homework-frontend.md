# Homework Assignment — Frontend

**Feature:** The Owner of a group reservation sees what each guest has selected and confirms a single consolidated order for the restaurant.
The backend for this feature is implemented in the backend track (`homework-backend.md`); you will implement the frontend.

**Design:** Figma PNG exports are in the `homework-design/` folder located next to this file (which also describes what from the mockup is included in the homework and what is not). Original: [Figma — EPAM IT Marathon 6.0](https://www.figma.com/design/P9rkBNLJZ3WabXSQX9b5DO/EPAM-IT-Marathon-6.0?node-id=62645-20592) → page *FOR DEVELOPERS* → section **Owner Flow**.

**Tests are already written:** in `frontend/restaurant-frontend-app/homework-tests/` — provided separately for the practice task and each level. At the beginning of each level, copy its tests into `src/app/` and run `npm run test:hw` — initially, they will be red. Write code until they turn green. See `homework-tests/README.md` for copying instructions.

**Where to write code:** `frontend/restaurant-frontend-app`, following the rules in `frontend/restaurant-frontend-app/CLAUDE.md` (where to place files, naming conventions, styles).

## Getting Started

```bash
# extract the archive and open the project directory
cd frontend/restaurant-frontend-app
npm ci
npm start          # http://localhost:4200
```

The archive already includes:
- API models — `src/app/models/group-orders/`;
- Static mock data for layout — `src/app/shared/mocks/group-orders/group-order.preview.ts`;
- **Mock backend** (runs only during `npm start`) — `src/app/shared/mocks/group-orders/`. It handles requests for the demo reservation `demo-reservation` according to the rules in `homework-backend.md`. All other requests go to the real API;
- **Mock login** — `src/app/shared/mocks/auth/demo-auth.ts`: add `?demo` to any URL, and you are logged in as the Owner of the demo reservation. No registration or real account is required;
- **Tests for each level** — in `homework-tests/` and runnable via `npm run test:hw`;
- Pre-built `consolidatePreOrder()` function with unit tests — `src/app/shared/helpers/consolidate-pre-order.ts`: calculates the consolidated order for the restaurant (needed in Level 2).

**Levels 1–2 run entirely on mocks:** no need to run backend services, register, or log in. Open:

```
http://localhost:4200/reservations/demo-reservation/group-order?demo
```

A real backend is only needed for Level 3.

---

## Practical Assignment (~15 min)

`practice-frontend.md` — participant card and page listing all participants, step by step with ready code. **Tests:** `homework-tests/practice/` (8 tests). This forms the foundation for Level 1: after completing the practice, you will already have the route, page, and card component.

---

## Level 1 — Owner Review Screen Layout

Align the page from the practical assignment with the mockup **using static data** — `homework-design/01-owner-review.png`. No HTTP requests — strictly HTML, SCSS, and components.

**Tests:** copy `homework-tests/level-1/` into `src/app/` (5 page tests + 8 card tests from practice).

**What to add on top of the practice:**
- "Group Pre-Order" header: address, date, time, "4 of 5 guests" — from `GROUP_ORDER_PREVIEW.invite` (fields `LocationAddress`, `StartTime`, `EndTime`, `MaxGuestCount`; format date and time using the `date` pipe);
- Grand Total — `GROUP_ORDER_PREVIEW.sessions.total` formatted with the `currency` pipe;
- Page and card styling matching the mockup (`.scss` next to the component, `styleUrl` in `@Component`).

**Requirements:**
- Semantics: `<main>`, single `<h1>`, participants list as `<ul>`/`<li>`, card as `<article>`;
- Cart status is communicated not just by color;
- Colors from `app/shared/styles/abstracts/colors` (`@use … as color`, do not use `@import`);
- Responsive layout that does not break at 375 px width (mobile viewport);
- `npm run test:hw` — all practice and Level 1 tests are green.

Verify at: `http://localhost:4200/reservations/demo-reservation/group-order?demo`.

---

## Level 2 — Group Pre-Order on Mocks

All screen logic is driven by the **mock backend** included in the archive. It implements the rules from `homework-backend.md`, so a real backend is not needed. Work with the demo reservation using the mock login: `http://localhost:4200/reservations/demo-reservation/group-order?demo`.

**Endpoints** (in dev mode, `demo-reservation` is served by the mock):
- `GET /reservations/{reservationId}/invite-token` → `GET /guest/invites/{token}` — already implemented in `GuestInviteService`. Use the response to get `GroupReservationId` and header details;
- **`GET /guest/groups/{groupReservationId}/sessions`** — participants and their carts;
- **`GET /reservations/{reservationId}/guest-session-id`** — Owner's own session ID;
- `GET /guest/groups/{groupReservationId}/status` — group status (`Open` / `Submitted` / `Closed`);
- **`POST /group-reservations/{groupReservationId}/submit`**, payload `{ "reservationId": "…" }`.

**What to do:**
- Create service `src/app/services/group-orders/group-orders.service.ts` with methods for each endpoint.
- The page reads `reservationId` from the URL and loads data instead of using `GROUP_ORDER_PREVIEW`. State managed via `signal()`, derived values via `computed()`.
- The Owner's own card is labeled **My Pre-Order** (`participant.id === guestSessionId`).
- "Consolidated pre-order" block: `computed(() => consolidatePreOrder(participants()))` — the helper is already in the archive, simply import and use it.
- **Grand Total = sum of the consolidated order**, not the `total` from the API: only submitted carts are included in the restaurant order.
- Yellow warning banner if there are unsubmitted participants: "N participants haven't submitted their cart yet — their dishes won't be included in the order".
- Status badge next to the title: Open → "Collecting pre-orders", Submitted → "Sent to Restaurant".
- **Submit Pre-Order** button → confirmation dialog using the shared `<app-modal>` → submit. On success: toast "Pre-order sent to the restaurant", status badge "Sent to Restaurant", screen becomes read-only (`homework-design/02-sent-to-restaurant.png`).

**Requirements:**
- States: loading, error with a "Try again" button, empty ("Participants haven't added any dishes yet");
- Submit button is disabled if submitted participants have no dishes, and while the request is in flight (double-click should not send duplicate requests);
- On submit error, the screen does not break, and the button can be clicked again. The error toast is already shown by `tokenInterceptor` — do not duplicate it;
- Exact strings checked by tests: button "Submit Pre-Order", dialog buttons "Submit" and "Cancel", badge "Collecting pre-orders" / "Sent to Restaurant", "Try again", "Group reservation not found";
- **Tests:** copy `homework-tests/level-2/` into `src/app/` — this will **replace** the Level 1 page test (now the page fetches data via the service). 5 service tests + 13 page tests: data loading, "My Pre-Order", **$540 instead of $760**, state handling, single Submit call. `npm run test:hw` — all green.

The mock resets on page refresh — you can test Submit as many times as you like.

---

## Level 3 — API Integration

Replace the mock with a **real API**: endpoints from `homework-backend.md` (built in the backend track), real authentication, and real reservations. In addition, keep the UI in sync with guest actions.

**What to run:** backend with the endpoints from `homework-backend.md` (marathon backend track) and set its URL in `src/environments/environment.development.ts` → `backendUrl` (the API Gateway stage URL, e.g. `https://abc123.execute-api.eu-central-1.amazonaws.com/develop`). Restart `npm start` after changing it.

There is no backend in the archive, and `backendUrl` is a placeholder: it is not needed for Levels 1–2 because everything goes through the mock. Therefore, other application pages (menu, reservations) will show errors without a backend — this is expected.

**How to test with real data:**
1. Sign out of the demo account (**Sign out** in the header): the real API will reject the demo token. Register or log in as a Customer and book a table for 2+ guests.
2. After booking (Book a Table), the confirmation modal displays an **Invite Link** — copy the `/join/…` link.
3. Open the link in another browser or incognito window — this is a guest: name → select dishes → submit cart.
4. Place your own pre-order as Owner (otherwise the backend will return 400 on submit).
5. Open your screen: `/reservations/<your-reservationId>/group-order`. The mock ignores real IDs — requests will go to the real API.

**What to do:**
- **Background synchronization:** while the group is `Open`, fetch fresh carts and status every 15 seconds. When a guest submits their cart, the Owner sees it without refreshing the page. Synchronization stops after Submit.
- No full-screen loader during background updates: the data on the screen simply updates in place.
- Timestamp line "Updated at 14:05:30" and a **Refresh** button to sync immediately.
- If background refresh fails, keep the latest loaded data and display "Couldn't refresh. Showing data from …". Do not show a toast for every failed background attempt: pass `HttpContext` with `SKIP_ERROR_TOAST` (`src/app/auth/skip-error-toast.context.ts`) in the request.
- Real backend errors on Submit (no submitted carts, no order) are shown via toast with the message from the API; the screen state remains stable.

**Requirements:**
- Timers do not duplicate: reloading (Try again) must not start a second timer, and leaving the page stops the timer;
- If the previous request has not yet responded, a new one must not start (hint: `exhaustMap`);
- `getSessions$(id, { silent: true })` and `getStatus$(id, { silent: true })` — optional second parameter for silent requests; "Updated at …" label, "Refresh" button, "Couldn't refresh…" on error;
- **Tests:** copy `homework-tests/level-3/` into `src/app/` (new `*.sync.spec.ts` files, Level 2 tests remain intact): 3 service tests + 6 synchronization tests with `fakeAsync` + `tick(15_000)`.

### Bonus: Remove Guest

**`DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}`** — response in `GET …/sessions` format.
- **Remove Guest** link/button in each guest's card (`type: 'Guest'`), hidden for the Owner; also hidden after submit.
- Confirmation via `<app-modal>` (`homework-design/03-remove-guest.png`): "Are you sure you want to remove **Alex** and their pre-order from this reservation? This action cannot be undone."
- On success, the list and totals update from the server response — through the same `computed()` signals.
- The button has an `aria-label` including the name: "Remove Alex"; in the card — `canRemove = input(false)` and `removeClick = output<Participant>()`, in the service — `removeSession$(groupId, guestSessionId)`.
- **Tests:** `homework-tests/level-3-bonus/` (1 + 5).

---

## For All Levels

- Standalone components, `inject()`, `input()`/`output()`, signals for state — avoid introducing new `Subject`/`BehaviorSubject`.
- Pre-submission checks — four commands:
  ```bash
  npx tsc -p tsconfig.app.json  --noEmit
  npx tsc -p tsconfig.spec.json --noEmit
  BACKEND_URL=https://example.com npm run build
  npm run test:hw    # homework tests (other project tests are outside the scope of this assignment — some are already failing on main)
  ```
- Include a brief description with your submission: what was implemented, screenshots of different screen states, whether AI tools were used, and how you verified the result.
