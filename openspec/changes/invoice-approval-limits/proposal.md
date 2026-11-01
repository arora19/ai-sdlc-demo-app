# Proposal

## Why

At proposal time, the application let either approval role approve any pending
invoice in one step. The assignment required approval authority to reflect
invoice value and a distinct Director decision above 500,000, so this proposal
captured the policy and implementation plan before behavior changed.

## What Changes

- Add approval limits of 50,000 for Managers and 500,000 for Directors, inclusive.
- Require two distinct approvers for invoices above 500,000, with the second approver a Director.
- Define a visible intermediate approval state and preserve the invoice's submitted business data when an approval attempt is denied or an invoice is rejected.
- Keep authorization, decision recording, API responses, and the approval UI consistent with the new lifecycle.
- Record the owner-approved role mapping, first-approver eligibility, lower-amount eligibility, intermediate state, and in-memory synchronization approach in the design and capability spec.

## Capabilities

### New Capabilities

- `invoice-approval-limits`: Role-based amount authority, staged approval for high-value invoices, and rejection/state-integrity behavior.

### Modified Capabilities

None. The repository has no existing OpenSpec capability specs.

## Impact

- Domain behavior and invoice state: `ApprovalService`, `Invoice`, `InvoiceStatus`, and repository update semantics.
- API authorization and decision endpoints: `ApprovalsController`, existing session roles, and standard service error mapping.
- Static approval UI: approval queue, progress/state visibility, error display, and role-dependent actions.
- Unit tests, Playwright E2E tests, mutation-testing tooling/results, AI-assisted PR review, and final documentation.

This proposal preserves the original planning context. Implementation and
verification progress is tracked in [tasks.md](./tasks.md); this proposal does
not imply that every tracked task is complete.
