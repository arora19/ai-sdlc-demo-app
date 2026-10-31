namespace InvoiceApp.Domain;

public interface IInvoiceRepository
{
    IReadOnlyList<Invoice> GetAll();
    Invoice? GetById(string id);
    IReadOnlyList<Invoice> GetByIds(IEnumerable<string> ids);
    Invoice Add(Invoice invoice);
    void Update(Invoice invoice);
    bool Remove(string id);
}

/// <summary>
/// Optional repository capability for serializing an invoice read/validate/write transition.
/// The callback receives an isolated working copy and it is persisted only if it returns normally.
/// </summary>
public interface IAtomicInvoiceRepository : IInvoiceRepository
{
    T ExecuteAtomically<T>(string invoiceId, Func<Invoice?, T> transition);
}
