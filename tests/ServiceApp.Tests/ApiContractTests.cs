using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using ServiceApp.Api.Controllers;
using ServiceApp.Core.Common;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Tests;

/// <summary>BUG-02: the legacy "pay without payment" endpoint must no longer mark bills paid.</summary>
public class ApiContractTests
{
    [Fact]
    public void Bug02_Legacy_pay_endpoint_returns_410_and_touches_no_service()
    {
        var bills = new Mock<IBillService>(MockBehavior.Strict);
        var payments = new Mock<IPaymentService>(MockBehavior.Strict);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore();

        var controller = new CustomerApiController(
            Mock.Of<IServiceRequestService>(), bills.Object, payments.Object,
            Mock.Of<IBillPdfService>(), Options.Create(new RazorpaySettings()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() }
            }
        };

        var result = Assert.IsType<ObjectResult>(controller.PayBill(7));

        Assert.Equal(StatusCodes.Status410Gone, result.StatusCode);
        Assert.IsAssignableFrom<ProblemDetails>(result.Value);
        bills.VerifyNoOtherCalls();
        payments.VerifyNoOtherCalls();
    }

    [Fact]
    public void Bug02_BillService_no_longer_exposes_PayBillAsync() =>
        Assert.Null(typeof(IBillService).GetMethod("PayBillAsync"));

    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.BusinessRule, 422)]
    [InlineData(ErrorType.Dependency, 502)]
    [InlineData(ErrorType.Unexpected, 500)]
    public void ErrorType_maps_to_status(ErrorType type, int status) =>
        Assert.Equal(status, ApiControllerBase.ToStatusCode(type));
}
