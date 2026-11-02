# Homework Tests

The tests are already written — you write code until they all turn green.

| Folder | Purpose | Test Count |
|---|---|---|
| `practice/` | practice: participant card | 8 |
| `level-1/` | level 1: page layout | 5 |
| `level-2/` | level 2: service and page with mocks | 5 + 13 |
| `level-3/` | level 3: synchronization | 3 + 6 |
| `level-3-bonus/` | bonus: Remove Guest | 1 + 5 |

## How to Use

1. **At the start of each level**, copy the contents of the level's folder into `src/app/` (overwrite files if prompted). The folder structure already matches.
   - macOS / Linux, from `frontend/restaurant-frontend-app`:
     ```bash
     cp -R homework-tests/practice/. src/app/
     ```
   - Windows PowerShell:
     ```powershell
     Copy-Item -Recurse -Force homework-tests\practice\* src\app\
     ```
   - or simply drag and drop the folders in your IDE.
2. Run the tests:
   ```bash
   npm run test:hw
   ```
   Initially, they will be **red** (or fail to compile because the files don't exist yet). That is expected.
3. Write code following `homework-frontend.md` and run `npm run test:hw` after each step. More tests will turn green.
4. The level is complete when all its tests are green.

`level-2/` **replaces** the page test from `level-1/` — in level 2, the page fetches data from the service instead of using static data. The card test from `practice/` remains.

## What the Tests Check

The tests verify what the user sees (text, buttons, modal dialogs), so they rely on specific names and labels defined in `homework-frontend.md`:
- Classes `ParticipantCardComponent`, `GroupOrderPageComponent`, and `GroupOrdersService` in the files specified in the assignment;
- Card inputs — `participant` (and `isMe` in level 2, `canRemove` / output `removeClick` for the bonus);
- Service methods — `getGroupInvite$`, `getSessions$`, `getOwnerSessionId$`, `getStatus$`, `submit$` (+ `removeSession$` for the bonus; in level 3, `getSessions$` and `getStatus$` accept a second parameter `{ silent: true }`);
- Button labels — "Submit Pre-Order", and in dialogs: "Submit", "Try again", "Refresh", "Remove Guest".

A failing (red) test is a hint: read its name and error message to see what is missing.
