import { Page } from '@playwright/test';

export class InvoiceApprovalsPage {
  constructor(private readonly page: Page) {}

  async goto() {
    await this.page.goto('/');
  }

  async signIn(username: string, password: string) {
    await this.page.getByLabel('Username').fill(username);
    await this.page.getByLabel('Password').fill(password);
    await this.page.getByRole('button', { name: 'Sign in' }).click();
  }

  async createInvoice(customerName: string, amount: number) {
    await this.page.getByLabel('Customer name').fill(customerName);
    await this.page.getByLabel('Amount').fill(String(amount));
    await this.page.getByRole('button', { name: 'Create invoice' }).click();
  }

  async submitInvoice(customerName: string): Promise<string> {
    const row = this.invoiceRow(customerName);
    const submitButton = row.getByRole('button', {
      name: /Submit invoice INV-\d+ for approval/
    });
    const label = await submitButton.getAttribute('aria-label');
    const invoiceId = label?.match(/INV-\d+/)?.[0];

    if (!invoiceId) {
      throw new Error(`Could not find the invoice ID for ${customerName}.`);
    }

    await submitButton.click();
    return invoiceId;
  }

  async openApprovals() {
    await this.page.getByRole('tab', { name: 'Approvals' }).click();
  }

  invoiceRow(customerName: string) {
    return this.page
      .getByRole('table', { name: 'All invoices' })
      .getByRole('row')
      .filter({ hasText: customerName });
  }

  pendingInvoice(customerName: string) {
    return this.page
      .getByRole('listitem')
      .filter({ hasText: customerName });
  }

  async attemptApproval(invoiceId: string): Promise<{ status: number; error: string }> {
    return this.page.evaluate(async (id) => {
      const session = JSON.parse(sessionStorage.getItem('session') ?? 'null');
      const response = await fetch(`/api/approvals/${id}/approve`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${session.token}`,
          'Content-Type': 'application/json'
        }
      });
      const body = await response.json();
      return { status: response.status, error: body.error };
    }, invoiceId);
  }
}
