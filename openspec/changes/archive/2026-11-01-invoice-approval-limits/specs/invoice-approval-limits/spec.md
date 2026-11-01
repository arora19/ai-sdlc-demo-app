# Spec Delta

## Purpose

Defines amount-based approval authority and the multi-approver lifecycle for high-value invoices while preserving submitted invoice data and keeping decisions observable.

## ADDED Requirements

### Requirement: Existing roles map to business approval authority only
For invoice approval only, the system SHALL treat the existing Approver role as Manager and the existing Admin role as Director; this mapping SHALL NOT change administrative or destructive permissions.

#### Scenario: Approver acts as Manager for approval
- **WHEN** a signed-in user with the existing Approver role attempts an invoice decision
- **THEN** the system evaluates the user's authority as a Manager without granting administrative permissions

#### Scenario: Admin acts as Director for approval without permission expansion
- **WHEN** a signed-in user with the existing Admin role attempts an invoice decision
- **THEN** the system evaluates the user's authority as a Director while preserving the existing scope of Admin-only permissions

### Requirement: Manager approval authority is capped at 50,000
The system SHALL allow a Manager to approve an invoice of 50,000 or less and SHALL prevent a Manager from approving an invoice above 50,000.

#### Scenario: Manager approves at the inclusive limit
- **WHEN** a Manager attempts to approve an otherwise eligible invoice for 50,000
- **THEN** the approval is accepted

#### Scenario: Manager is denied above the limit
- **WHEN** a Manager attempts to approve an otherwise eligible invoice for 50,001
- **THEN** the attempt is denied with a clear reason and the invoice state is unchanged

### Requirement: Director approval authority is capped at 500,000
The system SHALL allow a Director to approve an invoice of 500,000 or less, including invoices within the Manager limit, and SHALL prevent a Director from finalizing an invoice above 500,000 with a single approval.

#### Scenario: Director approves within the Manager limit
- **WHEN** a Director attempts to approve an otherwise eligible invoice for 50,000
- **THEN** the approval is accepted

#### Scenario: Director approves at the inclusive limit
- **WHEN** a Director attempts to approve an otherwise eligible invoice for 500,000
- **THEN** the approval is accepted

#### Scenario: A single Director approval cannot finalize above the limit
- **WHEN** a Director attempts to finalize an invoice for 500,001 with only one approval
- **THEN** the invoice is not finalized and the attempt returns a clear reason

### Requirement: Invoices above 500,000 require two distinct approvals
An invoice above 500,000 SHALL require two approvals by different users, and the second approval SHALL be made by a Director before the invoice is finalized.

#### Scenario: Either business role provides the first approval
- **WHEN** a Manager or Director records the first approval for an invoice of 500,001
- **THEN** the invoice enters AwaitingSecondApproval and is not reported as Approved

#### Scenario: Distinct Director completes the high-value approval
- **WHEN** a Director different from the first approver records the second approval for an invoice of 500,001
- **THEN** the invoice moves to Approved and both approval decisions are recorded

#### Scenario: The first user cannot provide the second approval
- **WHEN** the user who recorded the first approval attempts the second approval
- **THEN** the attempt is denied with a clear reason and every invoice field, including the first approval, remains unchanged

#### Scenario: A Manager cannot provide the second approval
- **WHEN** a Manager attempts the second approval
- **THEN** the attempt is denied with a clear reason and every invoice field, including the first approval, remains unchanged

### Requirement: Concurrent approvals preserve approval invariants
Concurrent approval requests SHALL be serialized so that the invoice cannot be finalized without two distinct approvers or without a Director as the second approver.

#### Scenario: Competing second-approval requests cannot bypass policy
- **WHEN** concurrent requests attempt to make the second decision on an invoice awaiting second approval
- **THEN** at most one valid distinct Director decision completes the invoice, and every rejected request leaves the complete state, including prior approvals, unchanged

### Requirement: Denied approval attempts do not mutate invoice state
When an approval attempt is denied by policy or lifecycle validation, the system SHALL return a clear reason and SHALL leave every invoice field unchanged, including its status, submitted data, decision metadata, and all previously recorded approvals.

#### Scenario: Amount-policy denial preserves the invoice
- **WHEN** an approval attempt exceeds the actor's approved authority
- **THEN** the response explains the denial and every field of the invoice equals its value before the attempt

#### Scenario: Lifecycle denial preserves the invoice
- **WHEN** an approval attempt targets an invoice that is not eligible for that approval step
- **THEN** the response explains the denial and every field of the invoice equals its value before the attempt

### Requirement: Rejection preserves submitted invoice data
Rejecting an invoice SHALL record the rejection outcome and its decision metadata without changing submitted business data, including the invoice amount and customer details.

#### Scenario: Rejection records outcome without rewriting submitted data
- **WHEN** an authorized approver rejects a pending invoice with a non-empty reason
- **THEN** the invoice is Rejected with its actor, decision time, and reason recorded, while its submitted customer details and amount remain unchanged

### Requirement: Approval progress is visible and actionable
The API and approval UI SHALL distinguish an invoice awaiting its first approval from an invoice in AwaitingSecondApproval, show its current progress, and offer only actions permitted by the approval authority rules.

#### Scenario: Intermediate invoice appears in the approval queue
- **WHEN** an invoice has one approval and awaits a different Director's second approval
- **THEN** the API exposes AwaitingSecondApproval and the UI presents the second-approval step instead of treating it as a new first approval

#### Scenario: A completed invoice is no longer actionable
- **WHEN** an invoice reaches Approved or Rejected
- **THEN** the approval UI does not offer another approval or rejection action for that invoice
