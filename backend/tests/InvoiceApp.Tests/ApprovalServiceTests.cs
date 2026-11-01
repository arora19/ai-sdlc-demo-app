using InvoiceApp.Api;
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
        string? FirstApprovedBy,
        DateTime? FirstApprovedAt,
        string? DecidedBy,
        DateTime? DecidedAt,
        string? DecisionNote);

    private static Invoice InvoiceWith(string id, InvoiceStatus status) =>
        new() { Id = id, CustomerName = "Test School", Amount = 100m, Status = status };

    private static ApprovalService ServiceFor(FakeInvoiceRepository repository) => new(repository);

    private static ApprovalService ServiceFor(IInvoiceRepository repository) => new(repository);

    private static InvoiceSnapshot Snapshot(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.CustomerName,
            invoice.Amount,
            invoice.Status,
            invoice.CreatedAt,
            invoice.FirstApprovedBy,
            invoice.FirstApprovedAt,
            invoice.DecidedBy,
            invoice.DecidedAt,
            invoice.DecisionNote);

    private sealed class NonAtomicCopyingInvoiceRepository : IInvoiceRepository
    {
        private readonly FakeInvoiceRepository _repository;

        public NonAtomicCopyingInvoiceRepository(params Invoice[] invoices) =>
            _repository = new FakeInvoiceRepository(invoices);

        public IReadOnlyList<Invoice> GetAll() =>
            _repository.GetAll().Select(Copy).ToList();

        public Invoice? GetById(string id)
        {
            var invoice = _repository.GetById(id);
            return invoice is null ? null : Copy(invoice);
        }

        public IReadOnlyList<Invoice> GetByIds(IEnumerable<string> ids) =>
            _repository.GetByIds(ids).Select(Copy).ToList();

        public Invoice Add(Invoice invoice) => _repository.Add(Copy(invoice));

        public void Update(Invoice invoice) => _repository.Update(Copy(invoice));

        public bool Remove(string id) => _repository.Remove(id);

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

    private static Invoice Persisted(FakeInvoiceRepository repository, string invoiceId) =>
        repository.GetById(invoiceId) ?? throw new InvalidOperationException("Expected invoice to be persisted.");

    private static Invoice Persisted(InMemoryInvoiceRepository repository, string invoiceId) =>
        repository.GetById(invoiceId) ?? throw new InvalidOperationException("Expected invoice to be persisted.");

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
        var repository = new FakeInvoiceRepository(InvoiceWith("INV-1", InvoiceStatus.Draft));

        var result = ServiceFor(repository).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Equal(InvoiceStatus.PendingApproval, Persisted(repository, "INV-1").Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var repository = new FakeInvoiceRepository(InvoiceWith("INV-1", InvoiceStatus.Approved));
        var before = Snapshot(Persisted(repository, "INV-1"));

        Assert.Throws<InvalidOperationException>(() => ServiceFor(repository).Submit("INV-1"));

        Assert.Equal(before, Snapshot(Persisted(repository, "INV-1")));
    }

    [Fact]
    public void Submit_UsesNonAtomicRepositoryFallbackToPersistPendingState()
    {
        var repository = new NonAtomicCopyingInvoiceRepository(InvoiceWith("INV-1", InvoiceStatus.Draft));

        var result = ServiceFor(repository).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Equal(InvoiceStatus.PendingApproval, repository.GetById("INV-1")?.Status);
    }

    [Fact]
    public void Submit_ThrowsWhenInvoiceDoesNotExist()
    {
        var repository = new NonAtomicCopyingInvoiceRepository();

        Assert.Throws<KeyNotFoundException>(() => ServiceFor(repository).Submit("INV-404"));
    }

    [Fact]
    public void Approve_SetsStatusApproverAndTimestamp()
    {
        var repository = new FakeInvoiceRepository(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = ServiceFor(repository).Approve("INV-1", "demo.approver", ApprovalAuthority.Manager);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.approver", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(Snapshot(result), Snapshot(Persisted(repository, "INV-1")));
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraftWithoutChangingAnyField()
    {
        var repository = new FakeInvoiceRepository(InvoiceWith("INV-1", InvoiceStatus.Draft));
        var before = Snapshot(Persisted(repository, "INV-1"));

        Assert.Throws<InvalidOperationException>(() =>
            ServiceFor(repository).Approve("INV-1", "demo.approver", ApprovalAuthority.Manager));

        Assert.Equal(before, Snapshot(Persisted(repository, "INV-1")));
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor(new FakeInvoiceRepository());

        Assert.Throws<KeyNotFoundException>(() =>
            service.Approve("INV-404", "demo.approver", ApprovalAuthority.Manager));
    }

    [Fact]
    public void Approve_UsesNonAtomicRepositoryFallbackToPersistApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 50_000m;
        var repository = new NonAtomicCopyingInvoiceRepository(invoice);

        var result = ServiceFor(repository).Approve("INV-1", "reviewer-1", ApprovalAuthority.Manager);
        var persisted = repository.GetById("INV-1");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal(InvoiceStatus.Approved, persisted?.Status);
        Assert.Equal("reviewer-1", persisted?.DecidedBy);
        Assert.NotNull(persisted?.DecidedAt);
    }

    [Fact]
    public void Approve_UnknownAuthority_DeniesHighValueFirstApprovalWithoutChangingAnyInvoiceField()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 500_001m;
        var repository = new FakeInvoiceRepository(invoice);
        var before = Snapshot(Persisted(repository, "INV-1"));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ServiceFor(repository).Approve("INV-1", "reviewer-1", (ApprovalAuthority)99));

        Assert.Equal(before, Snapshot(Persisted(repository, "INV-1")));
    }

    [Fact]
    public void Approve_ManagerAtLimit_ApprovesAndRecordsUsernameAsDecisionActor()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 50_000m;
        var repository = new FakeInvoiceRepository(invoice);

        var result = ServiceFor(repository).Approve("INV-1", "reviewer-1", ApprovalAuthority.Manager);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("reviewer-1", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(Snapshot(result), Snapshot(Persisted(repository, "INV-1")));
    }

    [Fact]
    public void Approve_ManagerAboveLimit_DeniesWithoutChangingAnyInvoiceField()
    {
        const string expectedMessage =
            "Invoice INV-1 amount 50,001 exceeds Manager approval limit of 50,000.";
        var repository = new FakeInvoiceRepository(InvoiceWithDecisionHistory("INV-1", 50_001m));
        var before = Snapshot(Persisted(repository, "INV-1"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ServiceFor(repository).Approve("INV-1", "reviewer-1", ApprovalAuthority.Manager));

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Equal(before, Snapshot(Persisted(repository, "INV-1")));
    }

    [Theory]
    [InlineData(50_000)]
    [InlineData(500_000)]
    public void Approve_DirectorAtSpecifiedLimits_ApprovesAndRecordsUsernameAsDecisionActor(decimal amount)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = amount;
        var repository = new FakeInvoiceRepository(invoice);

        var result = ServiceFor(repository).Approve("INV-1", "reviewer-1", ApprovalAuthority.Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("reviewer-1", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(Snapshot(result), Snapshot(Persisted(repository, "INV-1")));
    }

    [Fact]
    public void Approve_DirectorBelowManagerLimit_Approves()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 49_999.99m;
        var repository = new FakeInvoiceRepository(invoice);

        var result = ServiceFor(repository).Approve("INV-1", "reviewer-1", ApprovalAuthority.Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("reviewer-1", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(Snapshot(result), Snapshot(Persisted(repository, "INV-1")));
    }

    [Fact]
    public void Approve_SecondApprovalAt500000_DeniesWithoutChangingAnyInvoiceField()
    {
        var invoice = new Invoice
        {
            Id = "INV-1",
            CustomerName = "Submitted Customer",
            Amount = 500_000m,
            Status = InvoiceStatus.AwaitingSecondApproval,
            CreatedAt = new DateTime(2020, 10, 30, 8, 15, 0, DateTimeKind.Utc),
            FirstApprovedBy = "first-reviewer",
            FirstApprovedAt = new DateTime(2020, 10, 30, 9, 15, 0, DateTimeKind.Utc),
            DecidedBy = "prior.decision",
            DecidedAt = new DateTime(2020, 10, 30, 10, 15, 0, DateTimeKind.Utc),
            DecisionNote = "prior note"
        };
        var repository = new FakeInvoiceRepository(invoice);
        var before = Snapshot(Persisted(repository, "INV-1"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ServiceFor(repository).Approve("INV-1", "director-second", ApprovalAuthority.Director));

        Assert.Contains("does not exceed 500,000", exception.Message);
        Assert.Equal(before, Snapshot(Persisted(repository, "INV-1")));
    }

    [Theory]
    [InlineData(ApprovalAuthority.Manager, "manager-first")]
    [InlineData(ApprovalAuthority.Director, "director-first")]
    public void Approve_FirstApprovalAbove500000_RecordsNonFinalConcurrence(
        ApprovalAuthority authority,
        string firstApprover)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 500_001m;
        var repository = new FakeInvoiceRepository(invoice);

        var result = ServiceFor(repository).Approve("INV-1", firstApprover, authority);
        var persisted = Persisted(repository, "INV-1");

        Assert.Equal(InvoiceStatus.AwaitingSecondApproval, result.Status);
        Assert.NotEqual(InvoiceStatus.Approved, result.Status);
        Assert.Equal(firstApprover, result.FirstApprovedBy);
        Assert.NotNull(result.FirstApprovedAt);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        Assert.Null(result.DecisionNote);
        Assert.Equal(Snapshot(result), Snapshot(persisted));
    }

    [Fact]
    public void Approve_DistinctDirectorSecondApproval_FinalizesAndRetainsBothActorsAndTimes()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 500_001m;
        var repository = new FakeInvoiceRepository(invoice);
        var service = ServiceFor(repository);
        var firstApproval = service.Approve("INV-1", "manager-first", ApprovalAuthority.Manager);
        var firstApprovedAt = firstApproval.FirstApprovedAt;

        var result = service.Approve("INV-1", "director-second", ApprovalAuthority.Director);
        var persisted = Persisted(repository, "INV-1");

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("manager-first", result.FirstApprovedBy);
        Assert.Equal("director-second", result.DecidedBy);
        Assert.NotNull(firstApprovedAt);
        Assert.Equal(firstApprovedAt, result.FirstApprovedAt);
        Assert.NotNull(result.DecidedAt);
        Assert.NotNull(result.FirstApprovedAt);
        Assert.True(result.DecidedAt >= result.FirstApprovedAt);
        Assert.Equal(Snapshot(result), Snapshot(persisted));
    }

    [Theory]
    [InlineData(ApprovalAuthority.Manager)]
    [InlineData(ApprovalAuthority.Director)]
    public void Approve_SameUserSecondApproval_DeniesWithoutChangingAnyInvoiceField(
        ApprovalAuthority firstAuthority)
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 500_001m;
        var repository = new FakeInvoiceRepository(invoice);
        var service = ServiceFor(repository);
        service.Approve("INV-1", "same-reviewer", firstAuthority);
        var before = Snapshot(Persisted(repository, "INV-1"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.Approve("INV-1", "same-reviewer", ApprovalAuthority.Director));

        Assert.Contains("different from the first approver", exception.Message);
        Assert.Equal(before, Snapshot(Persisted(repository, "INV-1")));
    }

    [Fact]
    public void Approve_ManagerSecondApproval_DeniesWithoutChangingAnyInvoiceField()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 500_001m;
        var repository = new FakeInvoiceRepository(invoice);
        var service = ServiceFor(repository);
        service.Approve("INV-1", "director-first", ApprovalAuthority.Director);
        var before = Snapshot(Persisted(repository, "INV-1"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.Approve("INV-1", "manager-second", ApprovalAuthority.Manager));

        Assert.Contains("Only a Director", exception.Message);
        Assert.Equal(before, Snapshot(Persisted(repository, "INV-1")));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Approve_MalformedAwaitingSecondApproval_DeniesWithoutChangingAnyInvoiceField(
        bool includeFirstApprover,
        bool includeFirstApprovalTime)
    {
        var invoice = new Invoice
        {
            Id = "INV-1",
            CustomerName = "Submitted Customer",
            Amount = 500_001m,
            Status = InvoiceStatus.AwaitingSecondApproval,
            CreatedAt = new DateTime(2020, 10, 30, 8, 15, 0, DateTimeKind.Utc),
            FirstApprovedBy = includeFirstApprover ? "first-reviewer" : null,
            FirstApprovedAt = includeFirstApprovalTime
                ? new DateTime(2020, 10, 30, 9, 15, 0, DateTimeKind.Utc)
                : null,
            DecidedBy = "prior.decision",
            DecidedAt = new DateTime(2020, 10, 30, 10, 15, 0, DateTimeKind.Utc),
            DecisionNote = "prior note"
        };
        var repository = new FakeInvoiceRepository(invoice);
        var before = Snapshot(Persisted(repository, "INV-1"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ServiceFor(repository).Approve("INV-1", "director-second", ApprovalAuthority.Director));

        Assert.Contains("first approval", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, Snapshot(Persisted(repository, "INV-1")));
    }

    [Fact]
    public async Task Approve_ConcurrentSameUserSecondApproval_LeavesAwaitingStateAndHistoryUnchanged()
    {
        const int requestCount = 8;
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 500_001m;
        var repository = new InMemoryInvoiceRepository();
        repository.Add(invoice);
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "first-reviewer", ApprovalAuthority.Manager);
        var before = Snapshot(Persisted(repository, "INV-1"));

        var outcomes = await RunConcurrently(requestCount, _ =>
            service.Approve("INV-1", "first-reviewer", ApprovalAuthority.Director));

        Assert.All(outcomes, outcome => Assert.IsType<InvalidOperationException>(outcome));
        Assert.Equal(before, Snapshot(Persisted(repository, "INV-1")));
    }

    [Fact]
    public async Task Approve_ConcurrentDistinctDirectorSecondApprovals_OnlyOneTransitionWins()
    {
        const int requestCount = 8;
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        invoice.Amount = 500_001m;
        var repository = new InMemoryInvoiceRepository();
        repository.Add(invoice);
        var service = new ApprovalService(repository);
        service.Approve("INV-1", "manager-first", ApprovalAuthority.Manager);
        var before = Snapshot(Persisted(repository, "INV-1"));
        var actors = Enumerable.Range(1, requestCount).Select(index => $"director-{index}").ToArray();

        var outcomes = await RunConcurrently(requestCount, index =>
            service.Approve("INV-1", actors[index], ApprovalAuthority.Director));

        Assert.Single(outcomes, outcome => outcome is null);
        Assert.Equal(requestCount - 1, outcomes.Count(outcome => outcome is InvalidOperationException));

        var persisted = Persisted(repository, "INV-1");
        var after = Snapshot(persisted);
        Assert.Equal(InvoiceStatus.Approved, after.Status);
        Assert.Equal("manager-first", after.FirstApprovedBy);
        Assert.Equal(before.FirstApprovedAt, after.FirstApprovedAt);
        Assert.Contains(after.DecidedBy, actors);
        Assert.NotNull(after.DecidedAt);
        Assert.NotNull(after.FirstApprovedAt);
        Assert.True(after.DecidedAt >= after.FirstApprovedAt);
    }

    [Fact]
    public void Reject_RecordsReasonAndApproverWithoutChangingSubmittedData()
    {
        var invoice = InvoiceWithDecisionHistory("INV-1", 72_500m);
        var repository = new FakeInvoiceRepository(invoice);
        var before = Snapshot(Persisted(repository, "INV-1"));

        var result = ServiceFor(repository).Reject("INV-1", "demo.approver", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("demo.approver", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.NotEqual(before.DecidedAt, result.DecidedAt);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
        Assert.Equal(before.Id, result.Id);
        Assert.Equal(before.CustomerName, result.CustomerName);
        Assert.Equal(before.Amount, result.Amount);
        Assert.Equal(before.CreatedAt, result.CreatedAt);
        Assert.Equal(before.FirstApprovedBy, result.FirstApprovedBy);
        Assert.Equal(before.FirstApprovedAt, result.FirstApprovedAt);
        Assert.Equal(
            before with
            {
                Status = InvoiceStatus.Rejected,
                DecidedBy = "demo.approver",
                DecidedAt = result.DecidedAt,
                DecisionNote = "Duplicate invoice"
            },
            Snapshot(Persisted(repository, "INV-1")));
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var repository = new FakeInvoiceRepository(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));
        var before = Snapshot(Persisted(repository, "INV-1"));

        Assert.Throws<ArgumentException>(() =>
            ServiceFor(repository).Reject("INV-1", "demo.approver", "  "));

        Assert.Equal(before, Snapshot(Persisted(repository, "INV-1")));
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.AwaitingSecondApproval)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void Reject_NonPendingInvoice_DeniesWithoutChangingAnyInvoiceField(InvoiceStatus status)
    {
        var invoice = InvoiceWithDecisionHistory("INV-1", 72_500m);
        invoice.Status = status;
        if (status == InvoiceStatus.AwaitingSecondApproval)
        {
            invoice.FirstApprovedBy = "first-reviewer";
            invoice.FirstApprovedAt = new DateTime(2020, 10, 30, 9, 15, 0, DateTimeKind.Utc);
        }

        var repository = new FakeInvoiceRepository(invoice);
        var before = Snapshot(Persisted(repository, "INV-1"));

        Assert.Throws<InvalidOperationException>(() =>
            ServiceFor(repository).Reject("INV-1", "demo.approver", "Duplicate invoice"));

        Assert.Equal(before, Snapshot(Persisted(repository, "INV-1")));
    }

    private static async Task<Exception?[]> RunConcurrently(int requestCount, Action<int> request)
    {
        using var ready = new CountdownEvent(requestCount);
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, requestCount)
            .Select(index => Task.Run(() =>
            {
                ready.Signal();
                start.Wait();
                try
                {
                    request(index);
                    return null;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            }))
            .ToArray();

        try
        {
            Assert.True(ready.Wait(TimeSpan.FromSeconds(10)), "Concurrent requests did not reach the start gate.");
        }
        finally
        {
            start.Set();
        }

        return await Task.WhenAll(tasks);
    }
}
