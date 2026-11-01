---
name: invoice-approval-rules
description: "Use when implementing, reviewing, or testing invoice approval limits, approval sequencing, rejection, or Manager/Director authorization."
---

# Invoice approval rules

Use this skill before changing or reviewing invoice approval behavior. The owner-approved policy below is implemented; preserve it unless the human owner explicitly approves a policy change.

## Specified requirements

- Manager approval limit: 50,000.
- Director approval limit: 500,000.
- Invoices above 500,000 require two distinct approvers.
- The second approval for invoices above 500,000 must be made by a Director.
- Rejection must preserve the submitted invoice data, including its amount and customer details. Record the rejection outcome and decision metadata without changing that submitted data.

## Approved policy decisions

- `Approver` maps to Manager and `Admin` maps to Director for invoice approval only.
- The limits are inclusive. Either role may provide the first approval above 500,000; a different Director must provide the second.
- Rejection records its outcome and decision metadata while preserving submitted invoice data.

## Current implementation context

`ApprovalService` applies the approved limits and two-stage approval policy. Do not assume the in-memory repository provides durable or cross-process persistence.
