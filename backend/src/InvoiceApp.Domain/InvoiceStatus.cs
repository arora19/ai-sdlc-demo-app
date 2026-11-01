namespace InvoiceApp.Domain;

public enum InvoiceStatus
{
    Draft,
    PendingApproval,
    AwaitingSecondApproval,
    Approved,
    Rejected
}
