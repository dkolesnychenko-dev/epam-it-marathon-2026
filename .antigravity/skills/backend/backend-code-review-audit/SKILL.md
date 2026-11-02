---
name: backend-code-review-audit
description: >
  Conducts structured static analysis and architectural audits for backend .NET 10
  and AWS DynamoDB/Lambda code. Produces standardized review reports and verdicts.
---

# Backend Code Review Audit Skill (`backend-code-review-audit`)

## Purpose
Enforces consistent, rigorous evaluation of backend code modifications in `backend/restaurant-backend-app/` across four critical dimensions: Architecture, .NET 10 Idioms, AWS Serverless/DynamoDB, and Security.

---

## Audit Checklist

### 1. Architecture & Layering
- [ ] Handlers in `Handlers/` are thin; no direct DB SDK calls or heavy business algorithms.
- [ ] Business logic, validations, and domain rules reside in `Services/` with an interface `I{Domain}Service`.
- [ ] DynamoDB SDK calls and data access are isolated in `Repositories/`.
- [ ] DTOs in `Models/` are separate from DynamoDB `Entities/`.
- [ ] Route is registered in `RouteRegistry.cs` using `ActivatorUtilities.CreateInstance`.

### 2. .NET 10 & C# 13 Idioms
- [ ] **No blocking calls:** Zero usage of `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`. All I/O is async (`await Task`).
- [ ] **Nullability:** Proper handling of nullables with `<Nullable>enable</Nullable>` (null-coalescing `??`, null-conditional `?.`).
- [ ] **Modern C#:** Collection expressions (`[]`), primary constructors where appropriate, pattern matching.

### 3. AWS Serverless & DynamoDB Efficiency
- [ ] **No Unindexed Scans:** Flag any use of `ScanAsync` without strict justification. Prefer `QueryAsync` using partition keys and GSIs.
- [ ] **Index Alignment:** Key condition expressions match primary partition key or GSI partition key definitions.
- [ ] **Syndicate Packaging:** Any new Lambda has correct `lambda_config.json` with runtime `dotnet10` and least-privilege IAM policies.

### 4. Security & Error Handling
- [ ] **Tenant / User Isolation:** User cannot access or mutate resources belonging to another user without authorization.
- [ ] **Authentication:** Cognito claims/JWT checks verified.
- [ ] **Error Handling:** Custom exceptions mapped to standard responses via `ResponseBuilder.CreateErrorResponse`.
- [ ] **No Secret Leaks:** No API keys, credentials, or raw stack traces exposed in responses or logs.

---

## Verdict Determination

| Verdict | When to Return | Next Orchestration Action |
| :--- | :--- | :--- |
| **`APPROVED`** | Code meets all architectural, security, and performance standards. Zero critical bugs. | Proceed to final report. |
| **`CHANGES REQUESTED`** | Contains critical bugs, unindexed table scans, blocking calls, security flaws, or broken contracts. | Orchestrator sends feedback to `aws-lambda-agent` (max 3 loops). |
| **`NEEDS DISCUSSION`** | Ambiguity in business logic, trade-off between consistency and performance, or architectural conflict. | Orchestrator pauses pipeline and asks user for clarification. |

