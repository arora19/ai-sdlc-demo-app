using InvoiceApp.Api.Auth;
using InvoiceApp.Api.Controllers;
using InvoiceApp.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalsControllerTests
{
    [Theory]
    [InlineData("Approver", "manager-reviewer", 50_000)]
    [InlineData("Admin", "director-reviewer", 500_000)]
    public void Approve_MapsSessionRoleToAuthorityAndRecordsUsername(
        string role,
        string username,
        decimal amount)
    {
        var invoice = PendingInvoice(amount);
        var repository = new FakeInvoiceRepository(invoice);
        var controller = ControllerFor(repository, new UserSession(username, role));

        var result = controller.Approve(invoice.Id);

        var approved = Assert.IsType<Invoice>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(InvoiceStatus.Approved, approved.Status);
        Assert.Equal(username, approved.DecidedBy);
        Assert.Equal(InvoiceStatus.Approved, repository.GetById(invoice.Id)?.Status);
        Assert.Equal(username, repository.GetById(invoice.Id)?.DecidedBy);
    }

    [Theory]
    [InlineData("Approver", "manager-reviewer", 50_001, "Invoice INV-1 amount 50,001 exceeds Manager approval limit of 50,000.")]
    public void Approve_OverLimitReturnsConflictAndPreservesInvoice(
        string role,
        string username,
        decimal amount,
        string expectedMessage)
    {
        var invoice = PendingInvoice(amount);
        invoice.DecidedBy = "previous-reviewer";
        invoice.DecisionNote = "previous note";
        var before = Snapshot(invoice);
        var repository = new FakeInvoiceRepository(invoice);
        var controller = ControllerFor(repository, new UserSession(username, role));

        var result = controller.Approve(invoice.Id);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(expectedMessage, ErrorMessage(conflict.Value));
        Assert.Equal(before, Snapshot(repository.GetById(invoice.Id)!));
    }

    [Theory]
    [InlineData("Approver", "manager-reviewer")]
    [InlineData("Admin", "director-reviewer")]
    public void Approve_HighValueFirstApprovalReturnsAwaitingSecondApproval(
        string role,
        string username)
    {
        var invoice = PendingInvoice(500_001m);
        var repository = new FakeInvoiceRepository(invoice);
        var controller = ControllerFor(repository, new UserSession(username, role));

        var result = controller.Approve(invoice.Id);

        var awaiting = Assert.IsType<Invoice>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(InvoiceStatus.AwaitingSecondApproval, awaiting.Status);
        Assert.Equal(username, awaiting.FirstApprovedBy);
        Assert.NotNull(awaiting.FirstApprovedAt);
        Assert.Null(awaiting.DecidedBy);
        Assert.Null(awaiting.DecidedAt);
        Assert.Equal(InvoiceStatus.AwaitingSecondApproval, repository.GetById(invoice.Id)?.Status);
        Assert.Equal(username, repository.GetById(invoice.Id)?.FirstApprovedBy);
    }

    private static ApprovalsController ControllerFor(FakeInvoiceRepository repository, UserSession session)
    {
        var controller = new ApprovalsController(new ApprovalService(repository));
        var httpContext = new DefaultHttpContext();
        httpContext.Items[HttpContextExtensions.SessionKey] = session;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static Invoice PendingInvoice(decimal amount) =>
        new() { Id = "INV-1", CustomerName = "Test School", Amount = amount, Status = InvoiceStatus.PendingApproval };

    private static string? ErrorMessage(object? value) =>
        value?.GetType().GetProperty("error")?.GetValue(value) as string;

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

    private sealed record InvoiceSnapshot(
        string Id,
        string CustomerName,
        decimal Amount,
        InvoiceStatus Status,
        DateTime CreatedAt,
        string? DecidedBy,
        DateTime? DecidedAt,
        string? DecisionNote);
}
