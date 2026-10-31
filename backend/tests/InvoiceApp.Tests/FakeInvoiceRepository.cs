using InvoiceApp.Domain;

namespace InvoiceApp.Tests;

internal class FakeInvoiceRepository : IAtomicInvoiceRepository
{
    private readonly object _lock = new();
    private readonly List<Invoice> _invoices = new();

    public FakeInvoiceRepository(params Invoice[] seed) => _invoices.AddRange(seed);

    public IReadOnlyList<Invoice> GetAll()
    {
        lock (_lock) return _invoices.ToList();
    }

    public Invoice? GetById(string id)
    {
        lock (_lock) return _invoices.FirstOrDefault(invoice => invoice.Id == id);
    }

    public IReadOnlyList<Invoice> GetByIds(IEnumerable<string> ids)
    {
        var wanted = ids.ToHashSet();
        lock (_lock) return _invoices.Where(invoice => wanted.Contains(invoice.Id)).ToList();
    }

    public Invoice Add(Invoice invoice)
    {
        lock (_lock)
        {
            _invoices.Add(invoice);
            return invoice;
        }
    }

    public void Update(Invoice invoice)
    {
        lock (_lock)
        {
            var index = _invoices.FindIndex(existing => existing.Id == invoice.Id);
            if (index >= 0) _invoices[index] = invoice;
        }
    }

    public T ExecuteAtomically<T>(string invoiceId, Func<Invoice?, T> transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        lock (_lock)
        {
            var index = _invoices.FindIndex(invoice => invoice.Id == invoiceId);
            if (index < 0)
            {
                return transition(null);
            }

            var workingCopy = Copy(_invoices[index]);
            var result = transition(workingCopy);
            _invoices[index] = workingCopy;
            return result;
        }
    }

    public bool Remove(string id)
    {
        lock (_lock) return _invoices.RemoveAll(invoice => invoice.Id == id) > 0;
    }

    private static Invoice Copy(Invoice invoice) =>
        new()
        {
            Id = invoice.Id,
            CustomerName = invoice.CustomerName,
            Amount = invoice.Amount,
            Status = invoice.Status,
            CreatedAt = invoice.CreatedAt,
            FirstApprovedBy = invoice.FirstApprovedBy,
            FirstApprovedAt = invoice.FirstApprovedAt,
            DecidedBy = invoice.DecidedBy,
            DecidedAt = invoice.DecidedAt,
            DecisionNote = invoice.DecisionNote
        };
}
