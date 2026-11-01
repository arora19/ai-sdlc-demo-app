# NAGP AI in SDLC Workshop Assignment

## Objective and delivered policy

This assignment extends the invoice-approval demo with value-based approval authority, a distinct second approval for high-value invoices, and observable, state-safe decisions. The policy is captured in the [approved invoice-approval-limits specification](openspec/specs/invoice-approval-limits/spec.md).

| Approval authority | Existing application role | Rule |
|---|---|---|
| Manager | `Approver` | May give a single final approval for amounts up to and including 50,000. |
| Director | `Admin` | May give a single final approval for amounts up to and including 500,000. |
| High-value invoice | Either role may approve first | Above 500,000, the first approval is non-final; a different user with Director authority must record the second approval. |

Thus, a Manager cannot approve 50,001 alone, a Director can approve 50,000 and 500,000, and neither can finalize an invoice above 500,000 with one approval. The role mapping applies only to invoice approval; it does not broaden other Admin permissions. A denied approval is not a rejection and leaves the invoice unchanged. A valid rejection records its outcome, actor, time, and reason without changing the submitted amount or customer details.

## Architecture, authorization, and limitations

The application is a .NET 8 ASP.NET Core API with a static web UI and no database. `ApprovalService` in the Domain project owns lifecycle and approval policy. `ApprovalsController` maps the signed-in session role to `Manager` or `Director`; the approval controller requires `Approver` or `Admin`, while existing destructive actions remain Admin-only. Session authentication remains in front of the API.

The singleton `InMemoryInvoiceRepository` implements `IAtomicInvoiceRepository`. For transitions it holds a repository lock, applies the service callback to an isolated invoice copy, and persists the copy only when the callback completes normally. The complete read/validate/write operation therefore serializes in this process, and rejected transitions do not partially mutate stored state. The same transition mechanism covers submission, approval, and rejection. Above the threshold, the intermediate `AwaitingSecondApproval` state retains the first approver and time; the final Director decision is recorded separately.

This synchronization is process-local, not a distributed transaction. Invoices and decisions are lost on restart and are not shared across instances. The demo has no durable audit store or production identity/role management. A deployment with multiple app instances or persistence needs an equivalent transactional or concurrency-control guarantee in its storage layer.

## Spec-driven workflow and incremental commits

The approved specification is maintained at [openspec/specs/invoice-approval-limits/spec.md](openspec/specs/invoice-approval-limits/spec.md). The completed change is archived at [openspec/changes/archive/2026-11-01-invoice-approval-limits/](openspec/changes/archive/2026-11-01-invoice-approval-limits/), with its [proposal](openspec/changes/archive/2026-11-01-invoice-approval-limits/proposal.md), [design](openspec/changes/archive/2026-11-01-invoice-approval-limits/design.md), and [task checklist](openspec/changes/archive/2026-11-01-invoice-approval-limits/tasks.md). All **22 of 22 tasks** are checked complete; strict OpenSpec validation passes.

The feature was deliberately split into two independently testable implementation commits:

1. `273e77f` — `feat: enforce role-based single-approver limits with verified rejection handling`. This phase implements role mapping, inclusive single-approver limits, and rejection/state-integrity behavior; it does not include the two-stage workflow.
2. `f20d589` — `feat: implement atomic two-approver invoice workflow with distinct director approval`. This later phase adds `AwaitingSecondApproval`, retained first-approval metadata, atomic transitions, and concurrency and multi-approver tests.

The subsequent history adds test/mutation evidence (`c2f4b12`), review remediations (`c622fde`, `5b32658`), sanitized logging evidence (`054abda`), expanded E2E coverage and task completion (`c0c2081`), then the OpenSpec archive (`458b73d`). The current feature-branch HEAD is `458b73d`.

## AI-assisted workflow and evidence

Repository-defined custom agents are [implementation-agent](.github/agents/implementation-agent.agent.md), [test-agent](.github/agents/test-agent.agent.md), and [review-agent](.github/agents/review-agent.agent.md). Repository skills include [invoice-approval-rules](.github/skills/invoice-approval-rules/SKILL.md), [test-quality](.github/skills/test-quality/SKILL.md), and the OpenSpec skills for [exploring](.github/skills/openspec-explore/SKILL.md), [proposing](.github/skills/openspec-propose/SKILL.md), [applying](.github/skills/openspec-apply-change/SKILL.md), [updating](.github/skills/openspec-update-change/SKILL.md), [syncing](.github/skills/openspec-sync-specs/SKILL.md), and [archiving](.github/skills/openspec-archive-change/SKILL.md). The sanitized log summary records workflow activity involving the implementation, test, and review agents and the invoice-rule, test-quality, OpenSpec apply/update, and agent-customization skills.

The hook configuration at [.github/hooks/ai-interaction-logging.json](.github/hooks/ai-interaction-logging.json) records `SessionStart`, `SessionEnd`, `UserPromptSubmit`, `PreToolUse`, `PostToolUse`, `PostToolUseFailure`, `SubagentStart`, `SubagentStop`, and `Stop` through the repository logger. The [sanitized submission JSONL](logs/ai-interactions-submission.jsonl) contains **4,347 records across 57 session IDs**. Its [evidence summary](logs/ai-interactions-summary.md) documents event counts, captured tool/outcome, agent and skill metadata, and the sanitization method. Prompt and unstructured tool-result text is redacted in the export; the event stream is not a complete conversation transcript. The original private raw log is not linked as a submission artifact.

These logs are event evidence, not a complete conversation transcript: 30 records lack an event type, timestamp formats are mixed, and subagent start/stop events are not a complete trace. Pattern-based redaction cannot guarantee removal of every sensitive value. No model metadata is present, so this assignment makes no model claim. Review the [logging summary](logs/ai-interactions-summary.md) for additional scope and redaction limitations.

## PR review, remediation, and human acceptance

The AI-assisted findings and remediation evidence are summarized in [logs/evidence/pr-review-findings.md](logs/evidence/pr-review-findings.md). F-01 identified a submission/approval race; submission was moved to the atomic transition and a coordinated regression test was added. F-02 identified an unsafe non-atomic repository fallback; the service now fails explicitly when atomic repository support is absent. F-03 requested broader Playwright coverage: it was initially deferred, then completed in `c0c2081`, bringing the suite to five scenarios. F-04 identified the historical 86.67% mutation report as predating the final atomic transition; the historical context was clarified and a current-implementation report preserved. F-05 identified missing policy and persistence documentation; the README was updated.

N-01 identified the lack of a second Director demo identity; `demo.director` and a controller test were added. N-02 identified stale agent/OpenSpec guidance; the affected policy and workflow documentation was updated. N-03 identified races in test synchronization; tests now coordinate competing requests at the atomic boundary. The details and evidence are in the findings record.

The owner posted a final human acceptance comment on [PR #1](https://github.com/arora19/ai-sdlc-demo-app/pull/1), accepting the fixes and documented remaining limitations. No formal GitHub review submission was made; acceptance was recorded as a PR comment, not an `Approve` review.

## Verification and reproducible commands

Verified results for the final implementation: **40/40 backend tests passed**, **5/5 Playwright tests passed**, strict OpenSpec validation passed, and the current-implementation Stryker.NET mutation score is **88.89%**. The project test configuration is in [InvoiceApp.Tests.csproj](backend/tests/InvoiceApp.Tests/InvoiceApp.Tests.csproj) and [playwright.config.ts](e2e/playwright.config.ts); the local Stryker version is pinned in [.config/dotnet-tools.json](.config/dotnet-tools.json).

Run from the repository root:

```sh
dotnet test backend/InvoiceApp.sln

cd e2e
npm install
npx playwright install chromium
npm test
cd ..

openspec validate invoice-approval-limits --strict

dotnet tool restore
dotnet stryker \
  --project backend/src/InvoiceApp.Domain/InvoiceApp.Domain.csproj \
  --test-project backend/tests/InvoiceApp.Tests/InvoiceApp.Tests.csproj \
  --mutate "**/ApprovalService.cs" \
  --reporter Json \
  --output /tmp/stryker-current-head \
  --concurrency 2
```

The Stryker output path above keeps a rerun separate from the preserved reports: [current implementation report](logs/evidence/mutation-reports/current-head-2026-11-01/reports/mutation-report.json), [71.67% baseline report](logs/evidence/mutation-reports/baseline/reports/mutation-report.json), and [historical 86.67% report](logs/evidence/mutation-reports/historical-before-atomic-transition/reports/mutation-report.json). The **71.67%** measurement was taken after feature implementation but before focused `ApprovalService` test improvements; it is not a pre-implementation score. The **86.67%** report came after those focused test improvements but before the final atomic-transition implementation. No pre-implementation mutation score exists. The archived [mutation evidence](openspec/changes/archive/2026-11-01-invoice-approval-limits/mutation-testing-evidence.txt) records the commands, mutant outcomes, and timing for all three measurements.

## Demo video and remaining submission step

**Demo video:** `NAGP_AI_SDLC_Demo.mp4` (duration **9 minutes 54 seconds**) is included alongside the project inside the OneDrive submission ZIP.

The implementation, tests, OpenSpec tasks, archive, and demo video are included in the submission. Before sharing, use only the sanitized AI log artifacts linked here; do not submit the private raw log.
