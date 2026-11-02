---
name: route-register
description: >
  Registers new API request handlers, HTTP routes, and DI service/repository bindings
  inside RouteRegistry.cs for the api-handler Lambda. Use whenever a new endpoint,
  handler, service, or repository is created or updated in restaurant-backend-app.
---

# Route Registration Skill (`route-register`)

## Purpose
Automates the exact procedure for registering dependency injection (DI) components and HTTP endpoints inside `backend/restaurant-backend-app/dnapp/lambdas/api-handler/Routing/RouteRegistry.cs`.

Instead of manually navigating or guessing syntax, follow this deterministic runbook.

---

## Target File
`backend/restaurant-backend-app/dnapp/lambdas/api-handler/Routing/RouteRegistry.cs`

---

## Standard Procedure

### Step 1: Add Namespace Usings (if needed)
Ensure the required domain namespaces are imported at the top of `RouteRegistry.cs`:
```csharp
using APIHandler.Handlers.{Domain};
using APIHandler.Repositories.{Domain};
using APIHandler.Services.{Domain};
```

### Step 2: Register Repositories and Services in DI Container
Locate `BuildRoutes()` in `RouteRegistry.cs`:
1. **Repository Registration (under `// 2. Repositories`):**
   ```csharp
   services.AddSingleton<I{Domain}Repository, {Domain}Repository>();
   ```
2. **Service Registration (under `// 3. Services`):**
   ```csharp
   services.AddSingleton<I{Domain}Service, {Domain}Service>();
   ```

### Step 3: Register HTTP Route Handlers
Inside `BuildRoutes()`, locate the relevant route group function (e.g. `GetDishRoutes()`, `GetReservationAndOrderRoutes()`, or create a dedicated local function `Get{Domain}Routes()`):

Add the route entry using `ActivatorUtilities.CreateInstance<{HandlerClass}>(provider)`:

```csharp
new RouteHandler(
    new Route(HttpMethods.{GET|POST|PUT|DELETE|PATCH}, "{route_path}"),
    ActivatorUtilities.CreateInstance<{HandlerClassName}>(provider)
)
```

#### Parameterized Path Rules:
- Path parameters use `{paramName}` syntax matching `APIGatewayProxyRequest.PathParameters` (e.g. `/orders/{id}`, `/locations/{locationId}/dishes/{dishId}`).
- HTTP methods must reference `APIHandler.Constants.HttpMethods` (e.g., `HttpMethods.GET`, `HttpMethods.POST`, `HttpMethods.PUT`, `HttpMethods.DELETE`, `HttpMethods.PATCH`).

### Step 4: Verification
Verify that `RouteRegistry.cs` compiles cleanly by invoking the `dotnet-build-check` skill:
```powershell
dotnet build backend/restaurant-backend-app/restaurant-backend-app.sln
```
Ensure 0 errors and 0 warnings on modified lines.

