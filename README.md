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

These are demo accounts for a local, in-memory app. They are not real credentials.

## What it does

1. Create an invoice (it starts as a draft).
2. Submit it for approval.
3. An approver approves it, or rejects it with a reason.

Invoices move through Draft, Pending Approval, then Approved or Rejected.

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
