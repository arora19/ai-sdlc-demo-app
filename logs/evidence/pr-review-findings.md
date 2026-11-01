# Invoice approval limits review findings

## Source note

The exact independent-review artifact or review comments were not present in
the workspace, and there is no active pull request associated with this
checkout. The findings below preserve the descriptions and disposition details
available in the task request; they are summaries, not fabricated verbatim
quotations. Findings N-01 through N-03 and F-04 are proposed pending human
verification.

## Findings and proposed verdicts

### F-01 — Submission and approval can overwrite each other's state

**Finding:** Submission used a separate read/mutate/write sequence while
approval used the repository's atomic transition. Concurrent submission and
approval could therefore persist a stale submission state over a completed
approval.

**Proposed verdict: Pending human verification.** `ApprovalService.Submit` now uses the same atomic
repository transition as approval and rejection. There is no out-of-transaction
submission update. A deterministic regression test gates submission inside
the atomic callback, then races an approval and verifies the final persisted
state is Approved.

**Evidence:** [ApprovalService.cs](../../backend/src/InvoiceApp.Domain/ApprovalService.cs),
[ApprovalServiceTests.cs](../../backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs).
The full backend suite passes: 40 passed, 0 failed.

### F-02 — The non-atomic fallback does not provide cross-request atomicity

**Finding:** The fallback lock was local to an `ApprovalService` instance and
could not guarantee an atomic read/validate/write transition across multiple
service instances or other writers. Accepting a repository without the atomic
capability could falsely imply a concurrency guarantee.

**Proposed verdict: Pending human verification.** The instance-local fallback lock was removed.
Submission, approval, and rejection now fail explicitly with
`NotSupportedException` when the repository does not implement
`IAtomicInvoiceRepository`; the service does not silently proceed or claim
cross-request atomicity. Regression tests verify the non-atomic submit and
approve paths reject the operation without changing invoice state.

**Evidence:** [ApprovalService.cs](../../backend/src/InvoiceApp.Domain/ApprovalService.cs),
[ApprovalServiceTests.cs](../../backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs).
The focused and full backend suites pass.

### F-03 — Additional Playwright scenarios are missing

**Finding:** Broader Playwright coverage for the new approval policy and
dual-approval flow was proposed.

**Proposed verdict: Pending human verification.** No additional Playwright scenarios were added; broader E2E coverage remains deferred in this change.
The existing required rejected-over-limit E2E scenario passes, and the
remaining dual-approval behavior (including distinct Director completion,
same-user denial, Manager second-approval denial, and concurrent second
approvals) is covered by backend unit tests. Expanding E2E coverage is outside
the requested change.

**Evidence:** [manager-approval-limit.spec.ts](../../e2e/tests/manager-approval-limit.spec.ts)
passes with `cd e2e && npm test`; dual-approval coverage is in
[ApprovalServiceTests.cs](../../backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs).

### F-04 — The committed 86.67% mutation report predates the final atomic transition

**Finding:** The committed 86.67% Stryker report was generated before the final
atomic-transition implementation and does not measure the current
`ApprovalService.cs`.

**Proposed verdict: Pending human verification.** The 71.67% baseline and
86.67% historical report remain preserved and are explicitly labeled
historical. Stryker.NET 4.13.0 was rerun on the current source, reporting
88.89%; raw output is preserved separately.

**Evidence:** [mutation-testing-evidence.txt](../../openspec/changes/invoice-approval-limits/mutation-testing-evidence.txt);
[baseline raw report](./mutation-reports/baseline/reports/mutation-report.json)
(SHA-256 `124660990c1475771ad0f5baf45700a630af9a6b292e69693b4688a4d492f2a3`);
[historical 86.67% raw report](./mutation-reports/historical-before-atomic-transition/reports/mutation-report.json)
(SHA-256 `ca903e3b230d9f0012e0c729cbc71aeebc957b8ec9a732c5a61c9c6a8875b076`);
[current-head raw report](./mutation-reports/current-head-2026-11-01/reports/mutation-report.json)
(SHA-256 `a00e5e7e7947a0dc2a201579e1e59f56ac59006fe5459e82042a3ced29c42311`).

### F-05 — README does not explain the new approval policy and persistence

**Finding:** The README did not describe the Manager/Director limits, the
AwaitingSecondApproval stage, the distinct Director second approval, or the
in-memory persistence limitation.

**Proposed verdict: Pending human verification.** The README now documents the role mapping,
inclusive limits, first and second approval rules, intermediate state, and
restart/multi-instance limitations.

**Evidence:** [README.md](../../README.md), cross-checked against the approved
[OpenSpec behavior](../../openspec/changes/invoice-approval-limits/specs/invoice-approval-limits/spec.md).

### N-01 — Default demo cannot complete Director-first approval

**Finding:** Only one Director/Admin demo identity was configured, preventing a
Director-first invoice above 500,000 from being completed by a different
Director.

**Proposed verdict: Pending human verification.** Added the `demo.director`
Admin-role demo account without changing the existing role or distinct-user
rules. Added an API-controller regression test in which `demo.admin` records
the first approval and `demo.director` completes the second.

**Evidence:** [appsettings.json](../../backend/src/InvoiceApp.Api/appsettings.json),
[ApprovalsControllerTests.cs](../../backend/tests/InvoiceApp.Tests/ApprovalsControllerTests.cs),
[README.md](../../README.md). The shared `ChangeMe123!` value is explicitly
identified as a non-sensitive local example, not a real credential.

### N-02 — Agent and OpenSpec documentation is stale

**Finding:** `AGENTS.md`, the approval skill, and OpenSpec proposal/change
README text described approval policy or implementation status as unresolved,
unimplemented, or planning-only.

**Proposed verdict: Pending human verification.** Updated those documents to
reflect the approved, implemented role mapping and policy, while retaining the
proposal's historical planning context and clarifying that remaining tracked
tasks are not complete.

**Evidence:** [AGENTS.md](../../AGENTS.md),
[invoice-approval-rules skill](../../.github/skills/invoice-approval-rules/SKILL.md),
[proposal.md](../../openspec/changes/invoice-approval-limits/proposal.md),
[change README](../../openspec/changes/invoice-approval-limits/README.md).

### N-03 — Concurrency tests did not coordinate at the atomic boundary

**Finding:** The previous race-test start gate could release calls before they
had reached the repository atomic transition.

**Proposed verdict: Pending human verification.** The regression tests now
hold competing calls at the `ExecuteAtomically` boundary and release them only
after every request has arrived. Dedicated tasks and bounded synchronization
replace timing-based assumptions; assertions cover rejected outcomes and the
persisted invoice snapshot/final state.

**Evidence:** [ApprovalServiceTests.cs](../../backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs).

## Verification

- `dotnet test backend/InvoiceApp.sln` — passed, 40 tests.
- `cd e2e && npm test` — passed, 1 Playwright test (rerun after implementation).
- Stryker.NET 4.13.0 on current `ApprovalService.cs` — 88.89%; 46 killed,
  5 survived, 1 no coverage, 2 timed out, 7 compile errors, 18 ignored.
- `openspec validate invoice-approval-limits --strict` — valid.
- `git diff --check` — passed.
- No Git commit was created. The existing AI interaction log was not modified.

## Subsequent E2E update — 2026-11-01

The F-03 `Deferred` disposition above records the state of the review; it is
preserved and is not rewritten as a different PR verdict. The deferred
page-object Playwright coverage was subsequently added for the inclusive
Manager and Director limits, Manager denial, both first-approver paths above
500,000, hidden same-user and Manager second-approval denials, distinct
Director completion, and non-actionable completed invoices.

Verification after this follow-up:

- `cd e2e && npm test` — passed, 5 tests.
- `dotnet test backend/InvoiceApp.sln` — passed, 40 tests.
- `openspec validate invoice-approval-limits --strict` — valid.
- `git diff --check` — passed.

This follow-up does not claim that a pre-implementation mutation baseline
exists, change the existing PR review dispositions, or create a commit.
