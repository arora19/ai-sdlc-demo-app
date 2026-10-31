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

## Approval policy for future changes

These are assignment requirements for future approval work; they are not implemented by the current single-approver flow. Keep business rules in `ApprovalService` and add tests before changing behavior.

- A Manager's approval limit is 50,000.
- A Director's approval limit is 500,000.
- An invoice above 500,000 requires two distinct approvers, and the second approval must be by a Director.
- Rejection must not alter the submitted invoice data (for example, its amount or customer). Record the rejection outcome and its decision metadata without rewriting that data. If “without mutation” is intended to prohibit even outcome/decision metadata writes, get human clarification before implementation.

### Open design decisions — human input required

- Who is eligible to make the first approval on an invoice above 500,000? Do not infer this from the Director second-approval requirement.
- How do the business roles Manager and Director map to the existing `Approver` and `Admin` roles and demo accounts? Do not change authentication, role names, or demo access without an explicit decision.
- Confirm threshold inclusivity and role eligibility at and below the 50,000 and 500,000 limits before encoding boundary behavior.

Do not silently choose answers to these questions. Ask the human owner and document the decision before implementing or testing policy-dependent behavior.

## Human review expectations

- A human must resolve the open approval-policy and role-mapping decisions before implementation; agents must not make these decisions by assumption.
- Have a human review changes to approval rules, authorization, or persisted invoice decisions before treating them as accepted.
- Run the relevant tests and review the resulting diff. Review `logs/ai-interactions.jsonl` for sensitive content before sharing it.
- Preserve the existing repository-local logging hooks and logger under `.github/hooks/` unless a human explicitly requests changes to them.

## Demo accounts (not real credentials)

- `demo.approver` / `ChangeMe123!` (role Approver)
- `demo.admin` / `ChangeMe123!` (role Admin)

## Never

- Commit real credentials, tokens or customer data.
- Add business logic to `Program.cs` or controllers.
