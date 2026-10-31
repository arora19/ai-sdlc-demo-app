---
name: invoice-approval-rules
description: "Use when implementing, reviewing, or testing invoice approval limits, approval sequencing, rejection, or Manager/Director authorization."
---

# Invoice approval rules

Use this skill before changing or reviewing invoice approval behavior. These assignment requirements are not implemented by the current single-approver flow.

## Specified requirements

- Manager approval limit: 50,000.
- Director approval limit: 500,000.
- Invoices above 500,000 require two distinct approvers.
- The second approval for invoices above 500,000 must be made by a Director.
- Rejection must preserve the submitted invoice data, including its amount and customer details. Record the rejection outcome and decision metadata without changing that submitted data.

## Open decisions — do not assume

- Eligibility of the first approver for an invoice above 500,000 is unspecified.
- It is unspecified how Manager and Director map to the app's existing `Approver` and `Admin` roles or demo accounts.
- Confirm whether the stated limits are inclusive and which roles may approve invoices at and below each limit.
- If “rejection without mutation” is meant to prohibit writing rejection status or decision metadata too, get clarification; the current app records rejection status, actor, time, and reason.

Ask the human owner to decide any unresolved point before implementing it or writing tests that depend on its answer. Do not change authentication or demo accounts as a shortcut.

## Current implementation context

`ApprovalService` currently approves or rejects each pending invoice as a whole with one approver. Rejection updates status and decision metadata. Treat this as context, not as evidence that the new assignment policy has been implemented.
