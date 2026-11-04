# Homework: Test Automation (AQA) — Group Pre-Order 🚀

## Task Overview

The goal is to automate end-to-end (E2E) scenarios for the application: from user actions in the user interface to the resulting output they see. The assignment covers three levels of complexity.

### Track Selection

There is no pre-deployed shared application for testing: you will test what you have deployed yourself.

| What you implemented and deployed in previous HW | What to complete | What to test |
|---|---|---|
| Backend + Frontend (integrated), deployed | **Track 1**: UI tests for core scenarios + API tests for authorization and validation | Your own deployed application |

All layers (scenarios, steps, tasks and questions, page objects with locators and actions, and API clients if needed) are written by you for your specific test target.

**Configuration:** The repository contains no `.env`, passwords, or URLs. Copy `aqa/restaurant-aqa-framework/.env.example` to `.env` and fill it with **your own data** (URLs, credentials); `.env` is listed in `.gitignore` and must not be committed. In CI, these values are configured via repository variables and secrets (see the framework's README).

**Submission:** Host your automated pipelines in your own public Git repository. **The link to this repository is your completed homework submission.**

**Architecture (your choice):**
- BDD + Screenplay (as in the provided framework);
- Screenplay without BDD;
- BDD with another pattern, e.g., Page/Component Object Model;
- Any other approach that you can justify.

Specify your chosen track and architecture approach at the beginning of your solution's README.

---

## Requirements

### Track 1: Group Pre-Order

Test **your own implementation** from the homework (Backend and/or Frontend). The reservation Owner invites guests, each guest builds their own order, and the Owner reviews everything together and confirms a single consolidated pre-order that is submitted to the restaurant.

Levels correspond to the BE/FE tasks: L1 — view participants, L2 — confirmation, L3 — participant removal.

**Test Pyramid:**
- **UI:** Core scenarios (Owner views participants and totals, confirms the pre-order, waiter views the order, participant removal).
- **API:** Authorization and validation (non-Owner, unauthorized user, non-existent group, foreign participant, duplicate submit) and data preparation (creating guests, populating their carts).

**Undefined Behavior Handling:**
Where the specification does not explicitly define an HTTP status code or error message (duplicate submit, non-Owner, removing the last participant), choose the behavior yourself, document it in your README, and test according to it. For a group with no participants, expect `participants: []` and `total: 0`.

---

### Level 1: Guest Sessions List

**Description:**
The Owner should be able to see which participants have joined the group order, what dishes they have added, the total amount for each participant, and the total amount for all participants.

**Scenarios to Automate:**
- The Owner creates a reservation and invites guests; guests join via the invitation and add dishes. The Owner sees all participants, their dishes, quantities, and individual totals.
- The grand total equals the sum of all participant totals and dynamically updates whenever a guest modifies their cart.
- A participant who has not yet confirmed their cart is present in the list with a corresponding status indicator; a participant with no dishes is also displayed.
- An empty group that no guests have joined yet.
- Access control: the list is visible only to the Owner of that group — not to another customer, guest, waiter, or unauthorized user.
- A group that does not exist.

---

### Level 2: Owner Session & Consolidated Submit

**Description:**
After reviewing the orders, the Owner confirms the consolidated pre-order to be sent to the restaurant. The Owner also has their own session within the group and can add their own dishes.

**Submission logic:**
- Take only carts that participants have confirmed (`submitted`).
- Aggregate all dishes into a single list; merge identical dishes with summed quantities.
- Dishes are added to the order, which receives the status `SubmittedByCustomer`.
- The group reservation receives the status `Submitted`.

**Submission restrictions:**
Submitting is not allowed if: there are no confirmed carts; confirmed carts contain no dishes; the order is not found; or the reservation/group does not exist. Only the Owner can confirm.

**Scenarios to Automate:**
- **Successful confirmation:** The Owner sees that the pre-order has been sent to the restaurant; identical dishes from different guests are merged and summed, unconfirmed carts are excluded.
- **Waiter view:** The waiter sees the confirmed order with the exact items and quantities confirmed by the Owner.
- **Error conditions:** Each condition under which submission is impossible must be covered by a separate scenario with the expected message/status.
- **Duplicate submission:** Submitting an already confirmed group a second time.
- **Access control:** Confirmation submitted by a non-Owner.

---

### Level 3: Guest Session Removal

**Description:**
The Owner can remove a participant's session from the group order; afterward, they see the updated participant list and recalculated total amount. Only a participant who belongs to this specific group can be removed, and only if they exist. Only the Owner can remove participants.

**Scenarios to Automate:**
- Removing one participant out of several: the rest remain, the total is recalculated, and the removed participant does not affect the total.
- Removing a participant who has already confirmed their cart; removing the last remaining participant.
- Participant belonging to another group (two independent groups, mismatched participants) — `400`.
- Participant that does not exist — `404`.
- Removal attempt by a non-Owner.
- **End-to-End flow:** Multiple guests add dishes → Owner reviews the list → Owner removes one participant → Owner confirms the pre-order → Waiter sees the order without the removed participant.

---

## Bonus Task (Star Task) ⭐

**Reporting and SDLC**

Set up test reporting in ReportPortal (can be hosted locally in a container or in the cloud). Deploy your CI/CD pipeline to AWS **or another cloud provider**.

---

## General Requirements

- **End-to-End Scenarios:** Tests are end-to-end: executed through the user interface the way a real user interacts with it. Test data preparation and state verification are permitted outside the UI (including via API, see the test pyramid).
- **Test Coverage:** For each level — both positive and negative scenarios; a single "happy path" is not enough.
- **Test Data Management:** Test data must be created before the test and cleaned up after it; test runs must not leave clutter in the target environment. If there is no cleanup API, use unique data for each run (prefix or GUID in email, distinct date and table).
- **Time-Dependent Data:** Generate time-sensitive data dynamically (`now + N`) to prevent flaky tests.
- **Test Independence:** Tests must be independent, must not rely on execution order, and must be capable of running in parallel.
- **Execution Reports & Artifacts:** A test execution report must be provided; for failed tests, provide investigation materials (screenshot, video, or trace).
- **CI/CD Integration:** Tests must run in CI with report generation and artifact collection; a failed test must not halt the pipeline.
- **Solution README:** Document your chosen track and architecture, setup and execution instructions, how to run test subsets (by tag or level), where to find test reports, and chosen behavior for cases not defined in the homework.
