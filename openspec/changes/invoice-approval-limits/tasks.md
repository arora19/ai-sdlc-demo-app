# Tasks

## 1. Policy baseline and mutation testing before improvements

- [x] 1.1 Verify the implementation plan matches the owner-approved policy in `design.md` and `specs/invoice-approval-limits/spec.md`; verify no task remains blocked on a human decision.
- [ ] 1.2 Add a pinned, repository-local Stryker.NET test-tool configuration without changing runtime dependencies; verify the tool installs and runs against the existing backend tests.
- [ ] 1.3 Run the mutation suite before implementation and preserve its report and score as the baseline; verify the existing backend test suite passes unchanged.

## 2. Single-approver implementation and unit tests

- [x] 2.1 Map existing `Approver` to Manager and `Admin` to Director only at the invoice-approval boundary; verify API/service tests cover both mappings and existing Admin-only destructive authorization remains unchanged.
- [x] 2.2 Enforce the inclusive single-approver limits; verify unit tests cover Manager approval at 50,000, Manager denial at 50,001, Director approval at 50,000 and 500,000, and denial for any single-approval finalization at 500,001.
- [x] 2.3 Add unit tests proving every denied approval attempt preserves the complete invoice snapshot and an actual rejection changes only outcome/decision metadata, not submitted customer data or amount; verify snapshots include decision metadata and any existing approval history.
- [x] 2.4 Run `dotnet test backend/InvoiceApp.sln`; verify all existing and new tests pass and invoices above 500,000 cannot be finalized in this phase.
- [ ] 2.5 Keep the single-approver phase in its own commit with no two-approver implementation; verify the commit is isolated and the backend suite passes at that commit.

## 3. Two-approver implementation and unit tests

- [ ] 3.1 Add `AwaitingSecondApproval` and retain the first approver identity/time plus the final Director decision in the in-memory invoice state; verify both Manager and Director can provide the first approval and that it is non-final.
- [ ] 3.2 Implement a repository-level atomic approval transition with synchronization so read, validation, and mutation are serialized for an invoice; verify the transition checks state and approver identity inside the critical section before changing any invoice field.
- [ ] 3.3 Add unit tests for 500,001 first approval, intermediate state, completion by a different Director, same-user denial, Manager second-approval denial, and complete before/after snapshots on every denied path; verify any previously recorded approval remains identical after denial.
- [ ] 3.4 Add deterministic concurrency tests for competing same-user and distinct-Director requests using synchronization barriers/latches; verify no interleaving can bypass distinct-user/Director rules and at most one valid second decision is persisted.
- [ ] 3.5 Run `dotnet test backend/InvoiceApp.sln`; verify all tests pass, the correct decision history is retained, and only a distinct Director second approval finalizes a high-value invoice.
- [ ] 3.6 Keep the two-approver phase in a separate commit from the single-approver phase; verify each commit can be checked out independently with its corresponding backend test suite passing.

## 4. Playwright E2E testing

- [ ] 4.1 Add page-object-based Playwright coverage for the approved role mapping, boundaries at 50,000, 50,001, 500,000, and 500,001, second-approval progress, distinct Director completion, denial messages, and non-actionable completed invoices; verify selectors use accessible roles/labels.
- [ ] 4.2 Run `cd e2e && npm test`; verify the full approval flow passes through the API and UI and the intermediate invoice is shown as awaiting a Director's second approval.

## 5. Mutation testing after improvements

- [ ] 5.1 Run the pinned mutation suite against the completed implementation and unit tests; verify the report is generated and compare killed/surviving mutants with the pre-implementation baseline.
- [ ] 5.2 Investigate surviving approval-policy/state-integrity mutants and add focused assertions only where behavior is genuinely untested; verify backend tests and mutation results are rerun and unresolved survivors are explained without weakening tests.

## 6. AI-assisted PR review

- [ ] 6.1 Request a read-only review from the repository `review-agent` for the single-approver and two-approver commits/PRs; verify findings cover policy, state transitions, authorization, synchronization, persistence, and regressions.
- [ ] 6.2 Address confirmed findings in the appropriate implementation phase and rerun `dotnet test backend/InvoiceApp.sln` plus relevant E2E tests; verify no review finding is silently waived.

## 7. Final documentation

- [ ] 7.1 Update the README and any directly affected user-facing docs with the approved role mapping, limits, high-value approval progress, rejection/denial distinction, and in-memory restart limitation; verify documented behavior matches the final spec and UI.
- [ ] 7.2 Record baseline and final mutation-test results and verify documented test commands run successfully; then archive the completed OpenSpec change and verify the resulting main spec and archive validate.
