# AI interaction logging evidence summary

## Scope and method

This summary describes the raw repository-local event log as it existed at the export snapshot. The raw `logs/ai-interactions.jsonl` was not rewritten or deleted. The configured lifecycle hooks can append normal audit-session events while tools are used; the submission JSONL is a point-in-time sanitized copy, not a live mirror.

The workspace attachment metadata listed 3,565 raw-log lines initially; the export snapshot contains 3,606 records. The configured hooks can append records as inspection tools run. This audit did not directly write to or truncate the raw log.

The configured verifier was run against a temporary log and passed. Exported JSONL was parsed line-by-line after redaction, and the post-export sensitive-pattern scan found no matches for the patterns listed below. No application code, tests, hooks, or assignment source artifacts were changed.

## Captured sessions and events

- Export snapshot: **3,606 records**, all valid JSON objects; **50 distinct session identifiers** were present and retained in the sanitized JSONL.
- Event timestamps span **2026-10-31T08:00:10.280000+00:00** through **2026-11-01T05:16:20.248000+00:00** (UTC). 3577 event timestamps are ISO strings and 29 are numeric epoch-millisecond values; timestamp formats are mixed.

| Event type | Records |
|---|---:|
| (missing event_type) | 29 |
| PostToolUse | 1,680 |
| PostToolUseFailure | 28 |
| PreToolUse | 1,709 |
| SessionEnd | 18 |
| SessionStart | 13 |
| Stop | 48 |
| SubagentStop | 31 |
| UserPromptSubmit | 50 |

The missing `event_type` records still contain other metadata, but cannot be reliably classified from the event name. Tool results include **1,678 success** and **28 failure** outcomes; pre-tool records do not themselves establish an outcome.

## What the log supports

- **Prompts:** 50 prompt records and 13 initial-prompt records are present. Prompt text is useful evidence of requests but is not a guaranteed complete transcript.
- **Tools and outcomes:** tool metadata appears in 3,417 records. Captured tool names include `Read` (1,966), `Bash` (525), `Grep` (372), `Edit` (196), `Glob` (172), `skill` (60), `Agent` (60), `read_agent` (12), `tool_search_tool` (8), `WebFetch` (8). Post-tool results and error fields exist where emitted; outcomes are explicitly recorded for 1,708 records.
- **Agents/subagents:** agent metadata appears in 1,810 records. Observed agent types include `explore`, `implementation-agent`, `review-agent`, `test-agent`; agent/tool invocation metadata also demonstrates the custom agents `implementation-agent`, `test-agent`, and `review-agent` (and the `explore` agent type). There are 31 `SubagentStop` records and 0 `SubagentStart` records. These records do not establish complete subagent start-to-finish traces or exact run counts.
- **Skills:** skill-tool records demonstrably name `agent-customization`, `implementation-agent`, `invoice-approval-rules`, `openspec-apply-change`, `openspec-update-change`, `test-quality`. Recorded workflow-related skills include `invoice-approval-rules`, `test-quality`, `openspec-apply-change`, `openspec-update-change`, and `agent-customization`. Event rows can represent tool lifecycle stages, so counts of matching rows should not be read as distinct invocations.
- **Model:** no model metadata is present in any of the 3,606 records. The model used cannot be verified from this log.
- **Human oversight:** user prompt events and two `AskUserQuestion` tool records appear. This supports that human interaction was part of the workflow, but the log does not provide a complete, structured record of human approvals/decisions; one ask-user result is absent. The final human acceptance decisions for findings F-01 through F-05 and N-01 through N-03 were posted as a comment on [GitHub PR #1](https://github.com/arora19/ai-sdlc-demo-app/pull/1); those decisions are not captured in the JSONL log.

## Workflow and review/remediation evidence

The recorded workflow and current assignment artifacts show AI-assisted work on the invoice-approval-limits change: policy clarification, implementation, focused test work, independent/review-agent passes, test execution, and iterative remediation. Observed agent types and skills are listed above; the log is evidence of selected events, not a full transcript.

The assignment review artifact `logs/evidence/pr-review-findings.md` records findings F-01 through F-05 and N-01 through N-03 with **AI-proposed dispositions**, evidence references, and a pending-human-verification caveat. It is not the record of final human decisions. The final human acceptance decisions for these findings are in the comment on [GitHub PR #1](https://github.com/arora19/ai-sdlc-demo-app/pull/1). The artifact's notes describe remediation around atomic state transitions, removal of a non-atomic fallback, deterministic concurrency tests, demo Director identity, and documentation. Its statement that no active PR or exact independent-review artifact was present reflects the artifact's original context; it does not supersede the subsequent PR comment or establish that its proposed verdicts were independently verified.

The OpenSpec task list records many implementation/test tasks complete, but retains unchecked work for phase-isolated commits, broader E2E coverage, requested AI-assisted PR review, and final archive/documentation verification. The evidence document reports 40 passing backend tests, one passing Playwright test, and 88.89% current-head mutation score; these are quoted assignment evidence, not tests rerun for this logging audit. The hook verifier was rerun for this audit.

## Redactions and remaining limitations

The export retains event structure, identifiers, and original timestamp values. It redacts sensitive-key values, local filesystem paths, email addresses, phone-like formatted values, bearer/provider/JWT-style tokens, credential assignments, private-key blocks, the local username, demo account identifiers, and the known demo password. In this snapshot the export redacted 4,864 private-path occurrences, 21 email occurrences, 15 local-username occurrences, 778 demo-account identifier occurrences, and 225 demo-password occurrences; the other listed classes had no matches. Environment-variable fields are omitted. These are conservative pattern-based redactions; they cannot prove that every possible personal or confidential value has been identified.

The raw log contains sensitive material and remains ignored/untracked; do not submit it. The sanitized export passed a post-export scan for the redaction patterns above. The raw log is not suitable for sharing.

Limitations: model metadata is absent; event names are missing on some records; timestamps have mixed formats; subagent starts are not captured despite stop records; prompt/tool records are not a complete conversation transcript; explicit human decisions are not consistently structured; and this log alone cannot prove all actions, reviews, or outcomes. Timestamp values may also reflect clock skew relative to the audit environment.

## Output files

- `logs/ai-interactions-submission.jsonl` — sanitized event snapshot.
- `logs/ai-interactions-summary.md` — this evidence summary.
