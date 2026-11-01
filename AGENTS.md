# Project conventions for AI agents

Invoice approval demo app: ASP.NET Core 8 Web API with a small static web UI.

## Layout

- `backend/src/InvoiceApp.Domain`: business rules, no web or storage code. `ApprovalService` owns the invoice lifecycle (Draft, PendingApproval, Approved, Rejected).
- `backend/src/InvoiceApp.Api`: controllers, sign-in (`Auth/`), in-memory repository, and the web UI under `wwwroot/`.
- `backend/tests/InvoiceApp.Tests`: xUnit tests, one test class per class under test, named `<ClassUnderTest>Tests`.
- `e2e`: Playwright end-to-end tests (`tests/`) and page objects (`pages/`).
- `docs/use-cases`: short briefs for features we may build.

## Conventions

- Controllers live in `Controllers/`, one per resource, route `api/[controller]`. Request bodies are `record` types declared inside the controller.
- Controllers inherit `ApiControllerBase` and use `Handle(...)` so service errors map to 404, 409 and 400.
- Every endpoint is behind sign-in. Restrict actions with `[RequireRole("...")]`: destructive actions are Admin only, approvals are Approver or Admin.
- Business rules go in the Domain project, never in controllers.
- Register services in `Program.cs`. Repositories are interfaces in Domain with an in-memory implementation in Api.
- Every new rule or endpoint needs tests. Use `FakeInvoiceRepository` in tests.
- Use clear names; no one-letter variables or `flag` parameters.
- End-to-end tests use page objects and role-based locators (`getByRole`, `getByLabel`), never CSS or XPath selectors.

## Commands

- Run the app: `dotnet run --project backend/src/InvoiceApp.Api` then open http://localhost:5000
- Run the tests: `dotnet test backend/InvoiceApp.sln`
- Run end-to-end tests: `cd e2e && npm install && npx playwright install chromium && npm test`

## Implemented approval policy

These are the owner-approved rules implemented by `ApprovalService`; keep business rules there and add tests for any proposed policy changes.

- A Manager's approval limit is 50,000.
- A Director's approval limit is 500,000.
- `Approver` maps to Manager and `Admin` maps to Director for invoice approval only.
- The limits are inclusive. Either role may provide the first approval above 500,000; a different Director must provide the second approval.
- Rejection records its outcome and decision metadata without changing submitted invoice data, including the amount and customer details.

### Approved policy decisions

- Either role may provide the first approval above 500,000; the second approver must be a different Director.
- `Approver` maps to Manager and `Admin` maps to Director for invoice approval only; this does not change other authorization.
- Both limits are inclusive, and Directors may also approve amounts within the Manager limit.

## Human review expectations

- Have a human review proposed changes to approval rules, authorization, or persisted invoice decisions before treating those changes as accepted.
- Run the relevant tests and review the resulting diff. Review `logs/ai-interactions.jsonl` for sensitive content before sharing it.
- Preserve the existing repository-local logging hooks and logger under `.github/hooks/` unless a human explicitly requests changes to them.

## Demo accounts (not real credentials)

- `demo.approver` / `ChangeMe123!` (role Approver)
- `demo.admin` / `ChangeMe123!` (role Admin)
- `demo.director` / `ChangeMe123!` (role Admin; second distinct Director demo identity)

These are non-sensitive example credentials for the local demo only. Never use them for a deployed or shared environment.

## Never

- Commit real credentials, tokens or customer data.
- Add business logic to `Program.cs` or controllers.
