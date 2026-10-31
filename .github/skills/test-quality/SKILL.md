---
name: test-quality
description: "Use when designing, writing, or reviewing unit, integration, and Playwright tests for the invoice approval application."
---

# Test quality

Use this skill when adding or reviewing tests in this repository.

## Procedure

1. Read `AGENTS.md` and the production behavior under test. For approval policy, also read the `invoice-approval-rules` skill.
2. Test observable behavior through the appropriate service, API, or UI layer. Follow existing test organization and use `FakeInvoiceRepository` for domain service tests.
3. Include relevant success and failure cases, state-transition guards, persistence effects, and exact threshold/identity boundaries once policy decisions are settled.
4. For multi-approver behavior, verify that the two approvers are distinct and that the second approval is from a Director. Do not invent first-approver eligibility.
5. For rejection, assert that submitted invoice data remains unchanged while testing only the outcome metadata specified by the agreed policy.
6. Keep tests deterministic and focused. Do not weaken assertions, remove failing coverage, or hide failures.
7. Report the exact commands run, pass/fail results, and behavior blocked by unresolved requirements.

## Repository commands

- Backend: `dotnet test backend/InvoiceApp.sln`
- End-to-end: `cd e2e && npm install && npx playwright install chromium && npm test`

Run the smallest relevant test set during development, then the documented suite when appropriate. Never report a test as passing if it was not run.
