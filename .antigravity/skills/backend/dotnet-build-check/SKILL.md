---
name: dotnet-build-check
description: >
  Executes dotnet build on restaurant-backend-app solution or test projects,
  checks compiler diagnostics, and returns concise pass/fail statuses with file-and-line errors.
---

# .NET Build Check Skill (`dotnet-build-check`)

## Purpose
Runs a disciplined build verification on the .NET 10 solution or projects, extracting exact compilation errors without flooding agent context with hundreds of lines of non-actionable build logs.

---

## Commands

### 1. Full Solution Build (Implementation Gate)
Run from workspace root:
```powershell
dotnet build backend/restaurant-backend-app/restaurant-backend-app.sln --nologo -clp:ErrorsOnly
```

### 2. Test Project Build
```powershell
dotnet build backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj --nologo -clp:ErrorsOnly
```

### 3. Detailed Diagnostics (Only on Failure)
If errors occur, re-run with minimal verbosity to isolate the problem:
```powershell
dotnet build backend/restaurant-backend-app/restaurant-backend-app.sln -v m --nologo
```

---

## Output Parsing & Return Contract

When using this skill, format the verification result cleanly:

### Success Case
```markdown
### 🟢 Build Check: PASS
- **Target:** `backend/restaurant-backend-app/restaurant-backend-app.sln`
- **Status:** Build succeeded (0 errors, 0 warnings).
```

### Failure Case
```markdown
### 🔴 Build Check: FAIL
- **Target:** `backend/restaurant-backend-app/restaurant-backend-app.sln`
- **Errors Found:**
  - `[Handlers/Orders/CreateOrderHandler.cs:42]` CS0246: The type or namespace name 'IOrderService' could not be found.
  - `[Routing/RouteRegistry.cs:115]` CS1503: Argument 2: cannot convert from 'int' to 'string'.
- **Required Action:** Fix missing imports/types before proceeding.
```

