# Green & Tasty — Frontend · IT Marathon 6.0 Homework

Frontend for the Green & Tasty application (Angular 22) — starter code for the homework assignment.
There is no backend in this archive: it is not needed for levels 1–2.

## Getting Started

Requires Node.js 22.22+ or 24.x (see `frontend/restaurant-frontend-app/.nvmrc`).

```bash
cd frontend/restaurant-frontend-app
npm ci
npm start
```

Open **http://localhost:4200/reservations/demo-reservation/group-order?demo**
(`?demo` is a mock login: no account or backend needed). You will build the page for this URL during the practice assignment; for now, you will be redirected to the home page. There will be errors loading the menu and locations — there is no backend in the archive, which is completely expected for the homework.

## Reading Materials

| File | Contents |
|---|---|
| `practice-frontend.md` | ~15 min practice: participant card and page — the starting point of the homework |
| `homework-frontend.md` | homework: 3 levels, tasks, requirements, tests |
| `homework-design/` | Figma design (PNG) + what parts of the mockup are included in the homework. Original: [Figma](https://www.figma.com/design/P9rkBNLJZ3WabXSQX9b5DO/EPAM-IT-Marathon-6.0?node-id=62645-20592) |
| `frontend/restaurant-frontend-app/homework-tests/` | **pre-written tests** for the practice and each level — copy them into `src/app/`, run `npm run test:hw` |
| `homework-backend.md` | API contract (the exact endpoints simulated by the mock) |
| `frontend/restaurant-frontend-app/CLAUDE.md` | project guidelines: file structure, styles, testing (convenient to provide to an AI assistant) |

## Pre-Submission Verification

```bash
cd frontend/restaurant-frontend-app
npx tsc -p tsconfig.app.json  --noEmit
npx tsc -p tsconfig.spec.json --noEmit
BACKEND_URL=https://example.com npm run build
npm run test:hw
```
