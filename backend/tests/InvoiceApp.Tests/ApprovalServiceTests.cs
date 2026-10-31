using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    private sealed record InvoiceSnapshot(
        string Id,
        string CustomerName,
        decimal Amount,
        InvoiceStatus Status,
        DateTime CreatedAt,
        string? DecidedBy,
        DateTime? DecidedAt,
        string? DecisionNote);

    private static Invoice InvoiceWith(string id, InvoiceStatus status) =>
        new() { Id = id, CustomerName = "Test School", Amount = 100m, Status = status };

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    private static InvoiceSnapshot Snapshot(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.CustomerName,
            invoice.Amount,
            invoice.Status,
            invoice.CreatedAt,
            invoice.DecidedBy,
            invoice.DecidedAt,
            invoice.DecisionNote);

    private static Invoice InvoiceWithDecisionHistory(string id, decimal amount) =>
        new()
        {
            Id = id,
            CustomerName = "Submitted Customer",
            Amount = amount,
            Status = InvoiceStatus.PendingApproval,
            CreatedAt = new DateTime(2020, 10, 30, 8, 15, 0, DateTimeKind.Utc),
            DecidedBy = "previous.actor",
            DecidedAt = new DateTime(2020, 10, 30, 9, 15, 0, DateTimeKind.Utc),
            DecisionNote = "previous decision note"
        };

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);

        var result = ServiceFor(invoice).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));
    }

    [Fact]
    public void Approve_SetsStatusApproverAndTimestamp()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Approve("INV-1", "demo.approver", ApprovalAuthority.Manager);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.approver", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraft()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);
        var before = Snapshot(invoice);
        var service = ServiceFor(invoice);

        Assert.Throws<InvalidOperationException>(() =>
            service.Approve("INV-1", "demo.approver", ApprovalAuthority.Manager));

        Assert.Equal(before, Snapshot(invoice));
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        Assert.Throws<KeyNotFoundException>(() =>
            service.Approve("INV-404", "demo.approver", ApprovalAuthority.Manager));
    }

    [Fact]
    public void Approve_ManagerAtLimit_ApprovesAndRecordsUsernameAsDecisionActor()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 50_000m;
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "reviewer-1", ApprovalAuthority.Manager);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("reviewer-1", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_ManagerAboveLimit_DeniesWithoutChangingAnyInvoiceField()
    {
        const string expectedMessage =
            "Invoice INV-1 amount 50,001 exceeds Manager approval limit of 50,000.";
        var invoice = InvoiceWithDecisionHistory("INV-1", 50_001m);
        var before = Snapshot(invoice);
        var service = ServiceFor(invoice);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.Approve("INV-1", "reviewer-1", ApprovalAuthority.Manager));

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(before, Snapshot(invoice));
    }

    [Theory]
    [InlineData(50_000)]
    [InlineData(500_000)]
    public void Approve_DirectorAtSpecifiedLimits_ApprovesAndRecordsUsernameAsDecisionActor(decimal amount)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = amount;
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "reviewer-1", ApprovalAuthority.Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("reviewer-1", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_DirectorBelowManagerLimit_Approves()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 49_999.99m;
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "reviewer-1", ApprovalAuthority.Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("reviewer-1", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_DirectorAboveSingleApprovalLimit_DeniesWithoutChangingAnyInvoiceField()
    {
        const string expectedMessage =
            "Invoice INV-1 amount 500,001 exceeds Director single-approval limit of 500,000.";
        var invoice = InvoiceWithDecisionHistory("INV-1", 500_001m);
        var before = Snapshot(invoice);
        var service = ServiceFor(invoice);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.Approve("INV-1", "reviewer-1", ApprovalAuthority.Director));

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(before, Snapshot(invoice));
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var invoice = InvoiceWithDecisionHistory("INV-1", 72_500m);
        var before = Snapshot(invoice);
        var service = ServiceFor(invoice);

        var result = service.Reject("INV-1", "demo.approver", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("demo.approver", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.NotEqual(before.DecidedAt, result.DecidedAt);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
        Assert.Equal(
            before with
            {
                Status = InvoiceStatus.Rejected,
                DecidedBy = "demo.approver",
                DecidedAt = result.DecidedAt,
                DecisionNote = "Duplicate invoice"
            },
            Snapshot(invoice));
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.approver", "  "));
    }
}
