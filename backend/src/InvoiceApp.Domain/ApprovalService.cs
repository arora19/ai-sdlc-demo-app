using System.Globalization;

namespace InvoiceApp.Domain;

/// <summary>
/// Moves invoices through submission, approval, and rejection, including staged approval
/// for invoices requiring a second distinct approver.
/// </summary>
public class ApprovalService
{
    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        return Transition(invoiceId, invoice =>
        {
            if (invoice.Status != InvoiceStatus.Draft)
            {
                throw new InvalidOperationException(
                    $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
            }

            invoice.Status = InvoiceStatus.PendingApproval;
            return invoice;
        });
    }

    public Invoice Approve(string invoiceId, string approver, ApprovalAuthority authority)
    {
        return Transition(invoiceId, invoice =>
        {
            EnsureKnownAuthority(authority);

            if (invoice.Status == InvoiceStatus.AwaitingSecondApproval)
            {
                if (authority != ApprovalAuthority.Director)
                {
                    throw new InvalidOperationException(
                        $"Only a Director may provide the second approval for invoice {invoiceId}.");
                }

                if (string.IsNullOrWhiteSpace(invoice.FirstApprovedBy) || invoice.FirstApprovedAt is null)
                {
                    throw new InvalidOperationException(
                        $"Invoice {invoiceId} has no recorded first approval and cannot receive a second approval.");
                }

                if (string.Equals(invoice.FirstApprovedBy, approver, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Invoice {invoiceId} requires a second approver different from the first approver.");
                }

                if (invoice.Amount <= 500_000m)
                {
                    throw new InvalidOperationException(
                        $"Invoice {invoiceId} is not eligible for a second approval because its amount does not exceed 500,000.");
                }

                invoice.Status = InvoiceStatus.Approved;
                invoice.DecidedBy = approver;
                invoice.DecidedAt = DateTime.UtcNow;
                return invoice;
            }

            EnsurePending(invoice);

            if (invoice.Amount > 500_000m)
            {
                invoice.Status = InvoiceStatus.AwaitingSecondApproval;
                invoice.FirstApprovedBy = approver;
                invoice.FirstApprovedAt = DateTime.UtcNow;
                return invoice;
            }

            var (limit, limitDescription) = authority switch
            {
                ApprovalAuthority.Manager => (50_000m, "Manager approval limit"),
                ApprovalAuthority.Director => (500_000m, "Director approval limit"),
                _ => throw new ArgumentOutOfRangeException(nameof(authority), authority, "Unknown approval authority.")
            };

            if (invoice.Amount > limit)
            {
                var amount = invoice.Amount.ToString("#,##0.############################", CultureInfo.InvariantCulture);
                var formattedLimit = limit.ToString("#,##0.############################", CultureInfo.InvariantCulture);
                throw new InvalidOperationException(
                    $"Invoice {invoiceId} amount {amount} exceeds {limitDescription} of {formattedLimit}.");
            }

            invoice.Status = InvoiceStatus.Approved;
            invoice.DecidedBy = approver;
            invoice.DecidedAt = DateTime.UtcNow;
            return invoice;
        });
    }

    public Invoice Approve(string invoiceId, string approver) =>
        Approve(invoiceId, approver, ApprovalAuthority.Manager);

    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }

        return Transition(invoiceId, invoice =>
        {
            EnsurePending(invoice);

            invoice.Status = InvoiceStatus.Rejected;
            invoice.DecidedBy = approver;
            invoice.DecidedAt = DateTime.UtcNow;
            invoice.DecisionNote = reason;
            return invoice;
        });
    }

    private T Transition<T>(string invoiceId, Func<Invoice, T> transition)
    {
        if (_repository is not IAtomicInvoiceRepository atomicRepository)
        {
            throw new NotSupportedException(
                "Invoice transitions require a repository that supports atomic execution.");
        }

        return atomicRepository.ExecuteAtomically(
            invoiceId,
            invoice => transition(invoice ?? throw new KeyNotFoundException($"Invoice {invoiceId} was not found.")));
    }

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }

    private static void EnsureKnownAuthority(ApprovalAuthority authority)
    {
        if (authority is not ApprovalAuthority.Manager and not ApprovalAuthority.Director)
        {
            throw new ArgumentOutOfRangeException(nameof(authority), authority, "Unknown approval authority.");
        }
    }
}
