# Invoice Approval Demo App

A small invoice approval app used for live AI-in-SDLC demos. It is deliberately simple: a .NET 8 API, a plain web UI, and an in-memory store, so it runs with one command and no database.

## Run it

```
dotnet run --project backend/src/InvoiceApp.Api
```

Open http://localhost:5000 and sign in:

| User | Password | Role |
| --- | --- | --- |
| `demo.approver` | `ChangeMe123!` | Approver |
| `demo.admin` | `ChangeMe123!` | Admin |
| `demo.director` | `ChangeMe123!` | Admin |

These are non-sensitive example credentials for a local, in-memory app. They
are not real credentials and must not be used in a deployed or shared environment.

## What it does

1. Create an invoice (it starts as a draft).
2. Submit it for approval.
3. An approver approves it, or rejects it with a reason.

For invoice approvals, the existing `Approver` role acts as a Manager and may
approve amounts up to 50,000 inclusive. The existing `Admin` role acts as a
Director and may approve amounts up to 500,000 inclusive. An invoice above
500,000 requires two distinct approvers: either role may provide the first
approval, then a different Director must provide the second approval.

The invoice moves from Draft to Pending Approval. After the first approval of
an invoice above 500,000, it enters `AwaitingSecondApproval` until the distinct
Director records the second approval; only then is it Approved. Otherwise it
can be Approved within the actor's limit or Rejected with a reason.

An approval attempt denied by an amount limit or lifecycle rule is not a
rejection decision: it leaves the invoice status, submitted details, and
decision history unchanged. An authorized rejection is an actual decision; it
sets the status to Rejected and records the rejecting user, decision time, and
reason while preserving the submitted customer details and amount.

Invoices and approval decisions are stored in memory only. They are lost when
the application restarts and are not shared across multiple app instances.

## Two-stage approval demo

1. Sign in as `demo.admin`, create an invoice for more than 500,000, and submit
   it for approval.
2. Open **Approvals** and record the first approval. The invoice should move
   to `AwaitingSecondApproval`, shown as **Awaiting Second Approval** in the
   invoice list.
3. Sign out, then sign in as the different Director account `demo.director`.
   Open **Approvals** and record the second approval.
4. Open **Invoices** and confirm the invoice status is **Approved**.

The two accounts have Director authority, but the second approval must come
from a different user than the first. The app stores invoices in memory, so
restarting it clears the demo data and approval history.

## Tests

```
dotnet test backend/InvoiceApp.sln
```

## AI interaction logging

Repository-local Copilot hooks in `.github/hooks/` append lifecycle events to
`logs/ai-interactions.jsonl`. Start a new VS Code Copilot session in this
repository to enable them. The hooks record prompts, tool calls and outcomes,
session identifiers, available agent/subagent metadata, and model metadata only
when the runtime supplies it. Missing metadata is omitted.

The logger does not read or record process environment variables. It also
redacts common secret fields and secret-like values in recorded text. Prompt
and tool data can still contain sensitive information, so treat the local log
as private and review it before sharing. The generated JSONL file is
gitignored.

`PreToolUse` is a Copilot command hook; if Python is unavailable or the log
cannot be written, Copilot may deny that tool call. Keep Python 3 available
and ensure the repository log directory is writable.

Verify the hook logger without changing the repository log:

```
python3 .github/hooks/verify_ai_interaction_logging.py
```

## Project layout

```
backend/
  src/InvoiceApp.Domain   business rules
  src/InvoiceApp.Api      controllers, sign-in, web UI (wwwroot)
  tests/InvoiceApp.Tests  xUnit tests
docs/use-cases            short feature briefs
AGENTS.md                 conventions for AI coding agents
```

## Use cases

- [UC-1 Bulk invoice approval](docs/use-cases/UC-1-bulk-approval.md)
- [UC-2 Partial approval](docs/use-cases/UC-2-partial-approval.md)
