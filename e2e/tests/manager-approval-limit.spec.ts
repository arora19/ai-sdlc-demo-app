import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';
import { InvoiceApprovalsPage } from '../pages/invoice-approvals-page';

test('Approver cannot approve a pending invoice over the Manager limit', async ({ page }) => {
  const app = new InvoiceApprovalsPage(page);
  const customerName = `Manager limit test ${randomUUID()}`;
  const amount = 50_001;

  await app.goto();
  await app.signIn('demo.approver', 'ChangeMe123!');
  await expect(page.getByRole('banner')).toContainText('demo.approver (Approver)');

  await app.createInvoice(customerName, amount);
  const invoiceId = await app.submitInvoice(customerName);
  await expect(app.invoiceRow(customerName)).toContainText('Pending Approval');

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
  await expect(pendingInvoice).toContainText('Only a Director may approve this amount.');

  await page.reload();
  const unchangedInvoice = app.invoiceRow(customerName);
  await expect(unchangedInvoice).toContainText(customerName);
  await expect(unchangedInvoice).toContainText('$50,001.00');
  await expect(unchangedInvoice).toContainText('Pending Approval');
});
