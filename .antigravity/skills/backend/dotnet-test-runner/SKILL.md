---
name: dotnet-test-runner
description: >
  Executes xUnit unit tests in APIHandler.Tests.csproj, filters by class or method,
  and formats pass/fail/skipped counts and failure stack traces for QA validation.
---

# .NET Test Runner Skill (`dotnet-test-runner`)

## Purpose
Automates running unit tests using `dotnet test`, extracting clear test statistics, and parsing assertion failures and stack traces to feed into the QA gate and fix loops.

---

## Commands

### 1. Run Target Test Class
```powershell
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj --filter "FullyQualifiedName~{TestClassName}" --nologo --logger "console;verbosity=normal"
```

### 2. Run Full Test Suite (Testing Gate)
```powershell
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj --nologo --logger "console;verbosity=minimal"
```

---

## Output Parsing & Return Contract

### Success Case
```markdown
### 🟢 Test Run: ALL PASSED
- **Suite:** `APIHandler.Tests`
- **Results:** Total: 28, Passed: 28, Failed: 0, Skipped: 0.
- **Duration:** 1.4s
```

### Failure Case
```markdown
### 🔴 Test Run: FAILED
- **Suite:** `APIHandler.Tests`
- **Results:** Total: 28, Passed: 26, Failed: 2, Skipped: 0.
- **Failures:**
  1. `APIHandler.Tests.Services.OrderServiceTests.CreateOrderAsync_ValidInput_ReturnsOrder`
     - **Exception:** `Xunit.Sdk.EqualException`
     - **Message:** Expected: "CONFIRMED", Actual: "PENDING"
     - **Stack:** at `OrderServiceTests.cs:line 64`
  2. `APIHandler.Tests.Handlers.CreateOrderHandlerTests.HandleAsync_InvalidBody_ReturnsBadRequest`
     - **Exception:** `Xunit.Sdk.ThrowsException`
     - **Message:** Expected `ValidationException` was not thrown.
     - **Stack:** at `CreateOrderHandlerTests.cs:line 92`
- **Route Action:** Feed failure details to `aws-lambda-agent` or fix test mock setups.
```

