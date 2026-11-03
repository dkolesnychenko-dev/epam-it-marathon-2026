# QA Homework Report: US11 — Guest Reservation and Menu View

Assignment: see [homework-qa.md](homework-qa.md).
Deliverables: [homework-qa.xlsx](homework-qa.xlsx) (sheet "Test cases" and sheet "Defect reports").

Scope: user story US11. The guest flow is: the Owner creates the reservation and sends the group link to the guest; the guest opens the link, enters a name, and then sees the menu.

---

## Task 1: Requirements analysis

I found two gaps in the requirements. Both can be solved by changing the acceptance criteria.

### Gap 1: Real-time updates are not defined

**Problem.** AC5 says the menu "reflects the current data at the time the Guest opens or refreshes the screen." It does not say:
- whether a guest who is already on the menu sees a dish change made by the Owner, without refreshing;
- what happens to a dish that is already in the guest's subcart when the dish is later marked On Stop or Out Of Stock.

Without these rules, the guest can keep a dish in the subcart that cannot be ordered, and the Owner and the guest may see different states.

**Solution (proposed AC additions):**
- The menu must refresh dish states automatically while the guest is on the page, at least every 30 seconds, without a full page reload.
- If a dish in the guest's subcart becomes On Stop or Out Of Stock, the dish stays in the subcart, is marked as unavailable with the same badge, and is excluded from the consolidated order. The guest sees a message: "Some dishes in your subcart are no longer available."
- The Owner sees the same dish state as the guest, with the same badge text.

### Gap 2: Behaviour of Out Of Stock / Sold Out items is not specified

**Problem.** AC4 covers only "On Stop" items. It does not say how items with the Out Of Stock state (shown as "Sold Out" in the Figma design) must look or behave. The names also differ across sources: the AC says "On Stop", the Figma design says "Sold Out", and the application shows "On stop" for the Owner and "Out Of Stock" for the guest.

**Solution (proposed AC additions):**
- Define two unavailable states with fixed badge text: "On stop" and "Out Of Stock". "Sold Out" in the design is the same state as "Out Of Stock".
- Both states: the card is greyed out, the [Pre-Order] button is disabled, the dish cannot be added to the subcart, and the badge uses the text defined above.
- The same badge text is shown to the Owner and to the guest.

---

## Task 2: Test case design

I wrote 10 test cases using the template (sheet "Test cases" in homework-qa.xlsx). Each test case covers one acceptance criterion or one gap:

| ID | Title | Covers |
|---|---|---|
| TC-01 | Guest sees the full menu grouped by category | AC1 |
| TC-02 | Available dish has an active [Pre-Order] button | AC3 |
| TC-03 | Guest adds an available dish to the subcart | AC3 |
| TC-04 | Dish marked "On Stop" shows the "On stop" badge to the guest | AC4, Gap 2 |
| TC-05 | Dish marked "Out Of Stock" is shown as unavailable | Gap 2 |
| TC-06 | Unavailable dish cannot be added to the subcart | AC4 |
| TC-07 | Menu shows current user states after refresh | AC5, Gap 1 |
| TC-08 | Sort control changes the order of dishes | AC1 (ordering) |
| TC-09 | Reservation details are shown above the menu | US11 context |
| TC-10 | Menu is not available when the reservation is not "Reserved" | Pre-conditions |

Each test case follows the steps from the guest's point of view: the guest opens the group link from the Owner, enters a name, and then opens the menu.

---

## Task 3: Test execution

I tested against the screenshots of the implemented application provided by the tester. I did not have live access to the test data, so some tests are not complete.

| Result | Count | Test cases |
|---|---|---|
| Passed | 8 | TC-01 - TC-03, TC-03 - T09 |
| Failed | 2 | TC-04 TC-10 |

The Failed results (TC-04, T-10) are described in the defect report below.

---

## Task 4: Defect report

One defect was found. It is described in sheet "Defect reports" of homework-qa.xlsx.

**DEF-01: Guest sees "Out Of Stock" badge instead of "On stop" for a dish marked On Stop by the Owner**
- Severity: Major. Priority: High.
- Environment: https://d21fik7wr2oipe.cloudfront.net/main
- Steps: the Owner marks "Spring Salad" as On Stop; the Owner shares the link; the guest opens the link, enters a name, and clicks [Browse menu]; the guest checks the badge on "Spring Salad".
- Expected: badge "On stop".
- Actual: badge "Out Of Stock". The dish is still disabled, so ordering is blocked, but the label is wrong. Data is saved correctly after refresh, so the problem is in how the guest view shows the state.
- Suggested fix: map the On Stop state to the "On stop" badge in the guest menu, the same way the Owner view does.

**DEF-02: Guest with a Cancelled reservation sees an error page instead of the static "expired" page**
- Severity: Low. Priority: Low.
- Environment: https://d21fik7wr2oipe.cloudfront.net/main
- Preconditions: the Owner has generated and shared a valid link; reservation status is "Cancelled".
- Steps: the guest opens the group link received from the Owner.
- Expected: redirect to the static page "This pre-order session has expired or is no longer active."
- Actual: the page shows "We couldn't load the invitation. This may be a temporary connection or server issue. Please try again." with a [Try again] button. An error toast reads "Group reservation is expired." (screenshot attached in the Excel report).
- Suggested fix: when the reservation is not active, the guest route should show the static expired page from the acceptance criteria, instead of the generic load-error view. The "Try again" button should not appear, because retrying cannot help.
