import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';
import { InvoiceApprovalsPage } from '../pages/invoice-approvals-page';

const password = 'ChangeMe123!';

test('Manager can approve an invoice at the inclusive 50,000 limit', async ({ page }) => {
  const app = new InvoiceApprovalsPage(page);
  const customerName = `Manager inclusive limit ${randomUUID()}`;
  await app.goto();
  await app.signIn('demo.approver', password);
  await expect(page.getByRole('banner')).toContainText('demo.approver (Approver)');

  const invoiceId = await app.createAndSubmitInvoice(customerName, 50_000);
  await app.openApprovals();
  await app.approvePendingInvoice(customerName, `Approve invoice ${invoiceId}`);

  await app.openInvoices();
  const completedInvoice = app.invoiceRow(customerName);
  await expect(completedInvoice).toContainText('Approved');
  await expect(completedInvoice).toContainText('$50,000.00');
  await expect(completedInvoice.getByRole('button')).toHaveCount(0);
  await app.openApprovals();
  await expect(app.pendingInvoice(customerName)).toHaveCount(0);
});

test('Manager is denied at 50,001 without changing the invoice', async ({ page }) => {
  const app = new InvoiceApprovalsPage(page);
  const customerName = `Manager limit denial ${randomUUID()}`;
  await app.goto();
  await app.signIn('demo.approver', password);
  await expect(page.getByRole('banner')).toContainText('demo.approver (Approver)');

  const invoiceId = await app.createAndSubmitInvoice(customerName, 50_001);
  await app.openApprovals();
  const pendingInvoice = app.pendingInvoice(customerName);
  await expect(pendingInvoice).toBeVisible();
  await expect(
    pendingInvoice.getByText('Only a Director may approve this amount.', { exact: true })
  ).toBeVisible();
  await expect(
    pendingInvoice.getByRole('button', { name: `Approve invoice ${invoiceId}` })
  ).toHaveCount(0);

  const approvalAttempt = await app.attemptApproval(invoiceId);
  expect(approvalAttempt).toEqual({
    status: 409,
    error: `Invoice ${invoiceId} amount 50,001 exceeds Manager approval limit of 50,000.`
  });

  await page.reload();
  await app.openInvoices();
  const unchangedInvoice = app.invoiceRow(customerName);
  await expect(unchangedInvoice).toContainText(customerName);
  await expect(unchangedInvoice).toContainText('$50,001.00');
  await expect(unchangedInvoice).toContainText('Pending Approval');
  await app.openApprovals();
  await expect(app.pendingInvoice(customerName)).toContainText('Awaiting first approval.');
});

test('Director can approve an invoice at the inclusive 500,000 limit', async ({ page }) => {
  const app = new InvoiceApprovalsPage(page);
  const customerName = `Director inclusive limit ${randomUUID()}`;
  await app.goto();
  await app.signIn('demo.admin', password);
  await expect(page.getByRole('banner')).toContainText('demo.admin (Admin)');

  const invoiceId = await app.createAndSubmitInvoice(customerName, 500_000);
  await app.openApprovals();
  await app.approvePendingInvoice(customerName, `Approve invoice ${invoiceId}`);

  await app.openInvoices();
  const completedInvoice = app.invoiceRow(customerName);
  await expect(completedInvoice).toContainText('Approved');
  await expect(completedInvoice).toContainText('$500,000.00');
  await expect(completedInvoice.getByRole('button')).toHaveCount(0);
  await app.openApprovals();
  await expect(app.pendingInvoice(customerName)).toHaveCount(0);
});

test('Manager first approval awaits a different Director for 500,001', async ({ page }) => {
  const app = new InvoiceApprovalsPage(page);
  const customerName = `Manager first approval ${randomUUID()}`;
  await app.goto();
  await app.signIn('demo.approver', password);
  await expect(page.getByRole('banner')).toContainText('demo.approver (Approver)');

  const invoiceId = await app.createAndSubmitInvoice(customerName, 500_001);
  await app.openApprovals();
  const pendingInvoice = app.pendingInvoice(customerName);
  await expect(pendingInvoice).toContainText('Awaiting first approval.');
  await app.approvePendingInvoice(
    customerName,
    `Record first approval for invoice ${invoiceId}`
  );

  await app.openInvoices();
  await expect(app.invoiceRow(customerName)).toContainText('Awaiting Second Approval');
  await expect(app.invoiceRow(customerName)).toContainText('$500,001.00');
  await app.openApprovals();
  await expect(pendingInvoice).toContainText('First of two approvals complete.');
  await expect(pendingInvoice).toContainText('First approver: demo.approver');
  await expect(pendingInvoice).toContainText('Awaiting a different Director.');
  await expect(
    pendingInvoice.getByRole('button', { name: `Record first approval for invoice ${invoiceId}` })
  ).toHaveCount(0);

  await app.signOut();
  await app.signIn('demo.admin', password);
  await expect(page.getByRole('banner')).toContainText('demo.admin (Admin)');
  await app.openApprovals();
  await app.approvePendingInvoice(
    customerName,
    `Record second approval for invoice ${invoiceId}`
  );

  await app.openInvoices();
  const completedInvoice = app.invoiceRow(customerName);
  await expect(completedInvoice).toContainText('Approved');
  await expect(completedInvoice).toContainText('$500,001.00');
  await expect(completedInvoice.getByRole('button')).toHaveCount(0);
  await app.openApprovals();
  await expect(app.pendingInvoice(customerName)).toHaveCount(0);
});

test('Director-first approval denies same-user and Manager second attempts without mutation', async ({ page }) => {
  const app = new InvoiceApprovalsPage(page);
  const customerName = `Director first approval ${randomUUID()}`;
  await app.goto();
  await app.signIn('demo.admin', password);
  await expect(page.getByRole('banner')).toContainText('demo.admin (Admin)');

  const invoiceId = await app.createAndSubmitInvoice(customerName, 500_001);
  await app.openApprovals();
  await app.approvePendingInvoice(
    customerName,
    `Record first approval for invoice ${invoiceId}`
  );

  const pendingInvoice = app.pendingInvoice(customerName);
  await expect(pendingInvoice).toContainText('Awaiting a different Director.');
  await expect(pendingInvoice).toContainText('First approver: demo.admin');
  await expect(
    pendingInvoice.getByRole('button', { name: `Record second approval for invoice ${invoiceId}` })
  ).toHaveCount(0);

  const sameDirectorAttempt = await app.attemptApproval(invoiceId);
  expect(sameDirectorAttempt).toEqual({
    status: 409,
    error: `Invoice ${invoiceId} requires a second approver different from the first approver.`
  });
  await app.openInvoices();
  await expect(app.invoiceRow(customerName)).toContainText('Awaiting Second Approval');
  await expect(app.invoiceRow(customerName)).toContainText('$500,001.00');
  await app.openApprovals();
  await expect(pendingInvoice).toContainText('First approver: demo.admin');

  await app.signOut();
  await app.signIn('demo.approver', password);
  await expect(page.getByRole('banner')).toContainText('demo.approver (Approver)');
  await app.openApprovals();
  await expect(pendingInvoice).toContainText('A Director must provide the second approval.');

  const managerAttempt = await app.attemptApproval(invoiceId);
  expect(managerAttempt).toEqual({
    status: 409,
    error: `Only a Director may provide the second approval for invoice ${invoiceId}.`
  });
  await app.openInvoices();
  await expect(app.invoiceRow(customerName)).toContainText('Awaiting Second Approval');
  await expect(app.invoiceRow(customerName)).toContainText('$500,001.00');
  await app.openApprovals();
  await expect(pendingInvoice).toContainText('First approver: demo.admin');

  await app.signOut();
  await app.signIn('demo.director', password);
  await expect(page.getByRole('banner')).toContainText('demo.director (Admin)');
  await app.openApprovals();
  await app.approvePendingInvoice(
    customerName,
    `Record second approval for invoice ${invoiceId}`
  );

  await app.openInvoices();
  const completedInvoice = app.invoiceRow(customerName);
  await expect(completedInvoice).toContainText('Approved');
  await expect(completedInvoice).toContainText('$500,001.00');
  await expect(completedInvoice.getByRole('button')).toHaveCount(0);
  await app.openApprovals();
  await expect(app.pendingInvoice(customerName)).toHaveCount(0);
});
