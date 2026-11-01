# Invoice approval limits review findings

## Source note

The exact independent-review artifact or review comments were not present in
the workspace, and there is no active pull request associated with this
checkout. The finding descriptions below preserve the five findings and
disposition details available in the task request; they are summaries, not
fabricated verbatim quotations.

## Findings and proposed verdicts

### F-01 — Submission and approval can overwrite each other's state

**Finding:** Submission used a separate read/mutate/write sequence while
approval used the repository's atomic transition. Concurrent submission and
approval could therefore persist a stale submission state over a completed
approval.

**Proposed verdict: Fixed.** `ApprovalService.Submit` now uses the same atomic
repository transition as approval and rejection. There is no out-of-transaction
submission update. A deterministic regression test gates submission inside
the atomic callback, then races an approval and verifies the final persisted
state is Approved.

**Evidence:** [ApprovalService.cs](../../backend/src/InvoiceApp.Domain/ApprovalService.cs),
[ApprovalServiceTests.cs](../../backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs).
The full backend suite passes: 39 passed, 0 failed.

### F-02 — The non-atomic fallback does not provide cross-request atomicity

**Finding:** The fallback lock was local to an `ApprovalService` instance and
could not guarantee an atomic read/validate/write transition across multiple
service instances or other writers. Accepting a repository without the atomic
capability could falsely imply a concurrency guarantee.

**Proposed verdict: Fixed.** The instance-local fallback lock was removed.
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

**Proposed verdict: Deferred.** No additional Playwright scenarios were added.
The existing required rejected-over-limit E2E scenario passes, and the
remaining dual-approval behavior (including distinct Director completion,
same-user denial, Manager second-approval denial, and concurrent second
approvals) is covered by backend unit tests. Expanding E2E coverage is outside
the requested change.

**Evidence:** [manager-approval-limit.spec.ts](../../e2e/tests/manager-approval-limit.spec.ts)
passes with `cd e2e && npm test`; dual-approval coverage is in
[ApprovalServiceTests.cs](../../backend/tests/InvoiceApp.Tests/ApprovalServiceTests.cs).

### F-04 — Mutation-testing baseline context and raw reports need preservation

**Finding:** Mutation evidence must retain available raw reports and accurately
state when its initial baseline was measured; it must not be presented as a
pre-implementation baseline.

**Proposed verdict: Fixed.** The evidence now explicitly describes the initial
measurement as before the focused ApprovalService unit-test improvements, not
before implementation. The raw baseline and final JSON reports were copied
verbatim from their available `/tmp` locations and their SHA-256 digests were
verified against the originals.

**Evidence:** [mutation-testing-evidence.txt](../../openspec/changes/invoice-approval-limits/mutation-testing-evidence.txt);
[baseline raw report](./mutation-reports/baseline/reports/mutation-report.json)
(SHA-256 `124660990c1475771ad0f5baf45700a630af9a6b292e69693b4688a4d492f2a3`);
[final raw report](./mutation-reports/final/reports/mutation-report.json)
(SHA-256 `ca903e3b230d9f0012e0c729cbc71aeebc957b8ec9a732c5a61c9c6a8875b076`).
The historical measurements were preserved, not rerun or represented as a
measurement of the current code.

### F-05 — README does not explain the new approval policy and persistence

**Finding:** The README did not describe the Manager/Director limits, the
AwaitingSecondApproval stage, the distinct Director second approval, or the
in-memory persistence limitation.

**Proposed verdict: Fixed.** The README now documents the role mapping,
inclusive limits, first and second approval rules, intermediate state, and
restart/multi-instance limitations.

**Evidence:** [README.md](../../README.md), cross-checked against the approved
[OpenSpec behavior](../../openspec/changes/invoice-approval-limits/specs/invoice-approval-limits/spec.md).

## Verification

- `dotnet test backend/InvoiceApp.sln` — passed, 39 tests.
- `cd e2e && npm test` — passed, 1 Playwright test.
- `openspec validate invoice-approval-limits --strict` — valid.
- No Git commit was created. The existing AI interaction log was not modified.
