using System.Reflection;
using InvoiceApp.Api.Auth;
using InvoiceApp.Api.Controllers;
using InvoiceApp.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace InvoiceApp.Tests;

public class InvoicesControllerTests
{
    [Fact]
    public void Delete_AdminPassesRoleFilterAndRemovesInvoice()
    {
        var invoice = PendingInvoice();
        var repository = new FakeInvoiceRepository(invoice);
        var controller = new InvoicesController(repository, new ApprovalService(repository));
        var context = ActionContextFor(controller, new UserSession("admin-user", "Admin"));

        DeleteRoleFilter().OnActionExecuting(context);

        Assert.Null(context.Result);
        Assert.IsType<NoContentResult>(controller.Delete(invoice.Id));
        Assert.Null(repository.GetById(invoice.Id));
    }

    [Fact]
    public void Delete_ApproverGetsForbiddenAndInvoiceRemains()
    {
        var invoice = PendingInvoice();
        var repository = new FakeInvoiceRepository(invoice);
        var controller = new InvoicesController(repository, new ApprovalService(repository));
        var context = ActionContextFor(controller, new UserSession("approver-user", "Approver"));

        DeleteRoleFilter().OnActionExecuting(context);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(context.Result).StatusCode);
        Assert.Same(invoice, repository.GetById(invoice.Id));
    }

    private static RequireRoleAttribute DeleteRoleFilter() =>
        typeof(InvoicesController)
            .GetMethod(nameof(InvoicesController.Delete))!
            .GetCustomAttribute<RequireRoleAttribute>()!;

    private static ActionExecutingContext ActionContextFor(InvoicesController controller, UserSession session)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[HttpContextExtensions.SessionKey] = session;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller);
    }

    private static Invoice PendingInvoice() =>
        new() { Id = "INV-1", CustomerName = "Test School", Amount = 100m, Status = InvoiceStatus.PendingApproval };
}
