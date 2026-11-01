# Design

## Context

The current `ApprovalService` approves or rejects each `PendingApproval` invoice in one step. The invoice has one decision actor/time/note, `InvoiceStatus` has no intermediate approval state, and the singleton in-memory repository returns mutable invoice references. The API authenticates `Approver` and `Admin` sessions and authorizes both for the approval controller; the UI only queues `PendingApproval` invoices. There is no existing OpenSpec capability spec or durable database.

See [proposal.md](./proposal.md) for motivation and [specs/invoice-approval-limits/spec.md](./specs/invoice-approval-limits/spec.md) for the externally visible behavior.

## Goals / Non-Goals

**Goals:**

- Enforce the approved role limits and require two distinct recorded approvals above 500,000.
- Make approval progress and denial reasons visible through the API and UI.
- Ensure denied attempts do not partially mutate an invoice and rejection does not rewrite submitted business data.
- Keep both implementation stages independently testable and safe to commit.

**Non-Goals:**

- Replacing the demo's in-memory repository with a database or adding cross-process persistence.
- Changing sign-in credentials, token format, or the existing Admin-only destructive actions.
- Implementing the feature as part of this planning update.

## Decisions

The following decisions were approved by the human owner on 2026-10-31.

### 1. Map existing roles to business approval roles only

Map the existing `Approver` session role to `Manager` and `Admin` to `Director` only for invoice approval authority. Preserve the existing role strings, sign-in accounts, and Admin-only administrative/destructive permissions.

This keeps current authentication and demo-account compatibility while preventing an approval-role mapping from broadening unrelated Admin permissions. Alternatives considered were renaming demo roles or introducing separately configured approval roles; both add migration/configuration surface without a requirement to change sign-in.

### 2. Apply inclusive approval limits to both roles

Managers may approve invoices through 50,000 inclusive. Directors may approve invoices through 500,000 inclusive, including invoices within the Manager limit. Above 500,000, no single approval can finalize the invoice.

This treats each stated value as the role's maximum authority and makes Director authority a broader superset. Tests must cover Manager 50,000 and 50,001, Director 50,000 and 500,000, and the high-value 500,001 path.

### 3. Allow either role to provide the first high-value approval

For an invoice above 500,000, either a Manager or Director may provide the first approval. It is recorded as concurrence only and places the invoice in `AwaitingSecondApproval`. A different user who is a Director must provide the second approval before the invoice becomes Approved.

This allows either business role to participate in the first review while preserving the explicit Director second-approval control and separation of duties. Tests must cover first approval by each mapped role, successful completion by a different Director, denial for the same user, and denial for a Manager as second approver.

### 4. Store both decisions in the existing in-memory invoice repository

Add the explicit `AwaitingSecondApproval` state and retain both approval actors and decision times in the invoice state maintained by the existing in-memory repository. Preserve submitted customer and amount data. The existing final decision fields may represent the final outcome, but must not replace or erase the first approval record.

This provides request-to-request state in the current demo without introducing a database. The trade-off is that approval records are lost on process restart and are not shared across multiple app instances; durable or distributed persistence is outside this change.

### 5. Make approval state transitions atomic

Add a repository-level atomic transition for an invoice and execute the approval's read, validation, and mutation as one synchronized operation. Use synchronization scoped to the invoice where practical; the check of current state and approver identity must occur inside the same critical section as the state update. Keep amount/role/lifecycle rules in `ApprovalService`; the repository synchronization protects the transition without moving business policy into storage code.

This closes the gap between the current repository's individually locked `GetById` and `Update` calls: locking those calls separately does not serialize the service's complete read/validate/write sequence. In-memory synchronization is appropriate for this single-process demo but is not a distributed lock. A multi-instance or durable deployment would need an equivalent transactional/optimistic-concurrency guarantee in its persistence layer.

Concurrency tests must race duplicate-user and distinct-Director requests against the same `AwaitingSecondApproval` invoice. They must prove that at most one valid transition completes, two different users are required, a Director is the second approver, and every rejected attempt leaves the complete invoice state unchanged, including any previously recorded approval. Tests for sequential denial must also compare full before/after state snapshots.

### 6. Keep denied approval attempts distinct from rejection decisions

Validate role, amount, lifecycle state, and approver identity before mutation. A denied approval attempt returns a clear reason and leaves all invoice fields unchanged, including status, submitted data, decision metadata, and prior approvals. An actual invoice rejection remains a recorded business outcome: set `Rejected` and decision metadata while preserving submitted business data.

Use the established API error mapping: authentication/authorization failures remain 401/403; an authenticated user requesting a disallowed lifecycle step or amount receives a 409 conflict with a clear error message. Test the response body and verify no mutation on every denial path.

### 7. Preserve API and UI compatibility while exposing progress

Keep the existing approve/reject endpoints and use `PendingApproval` for invoices awaiting their first decision. Expose `AwaitingSecondApproval` as a distinct status and make progress/next action visible in the API and UI. The UI must show the correct second-approval step and avoid offering actions to completed invoices.

A separate status is preferred over inferring the stage from a count alone because it makes filtering and client behavior explicit. There are no external API consumers in the inspected repository; if consumers outside this project exist, the new enum value must be communicated before deployment.

## Risks / Trade-offs

- **Existing `Admin` is broader than an approval role** → Restrict the mapping to approval authority and test that Admin-only destructive authorization remains unchanged.
- **The in-memory repository is not durable** → Document restart loss; require a separately approved storage change before promising durable audit retention.
- **Synchronization is process-local** → Use one process as assumed by the demo; use a transactional/distributed concurrency mechanism before multi-instance deployment.
- **A new status value can surprise clients** → Preserve existing endpoint behavior, update UI/status filtering, and document the added status.
- **Race tests can be nondeterministic if they rely on timing** → Coordinate requests with barriers/latches and assert persisted invariants rather than a particular thread ordering.
- **The initial mutation measurement was not pre-implementation** → Stryker.NET is now pinned as a local test tool; preserve the initial and 86.67% reports as historical measurements, and report a separate current-head result without treating the scores as like-for-like.

## Migration Plan

The following sequence records the original implementation plan; it does not
assert that every milestone or separate-commit boundary was achieved. The
available baseline was captured after feature implementation and before
focused ApprovalService test improvements, so it is not a pre-implementation
measurement.

1. The owner-approved policy is recorded in this design and the capability spec; no unresolved policy gate remains for this scope.
2. The original plan called for a passing test and mutation baseline before implementation; preserve the available historical reports with their actual measurement context.
3. Implement and test the single-approver limits as one independently passing commit; invoices above 500,000 must remain unfinalized in this phase.
4. Implement and test the second-approval lifecycle, explicit intermediate state, recorded decisions, and synchronized transitions as a separate independently passing commit.
5. Run Playwright E2E coverage, repeat mutation testing, request AI-assisted review of each implementation diff, then update user-facing documentation.

Rollback of the second implementation stage must leave the first-stage guard in place so invoices above 500,000 cannot be finalized by a single approver. Reverting the first stage is a separate decision and must not silently restore unrestricted approvals.
