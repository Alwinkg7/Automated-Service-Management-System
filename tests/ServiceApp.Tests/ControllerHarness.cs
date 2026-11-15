using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceApp.Tests;

/// <summary>Gives an API controller an authenticated user and the MVC services ProblemDetails needs.</summary>
public static class ControllerHarness
{
    private static readonly IServiceProvider Services = BuildServices();

    public static T As<T>(this T controller, string userId, string role) where T : ControllerBase
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
                RequestServices = Services
            }
        };
        return controller;
    }

    /// <summary>HTTP status of an action result (ObjectResult, StatusCodeResult, ...).</summary>
    public static int Status(this IActionResult result) => result switch
    {
        ObjectResult o => o.StatusCode ?? StatusCodes.Status200OK,
        StatusCodeResult s => s.StatusCode,
        _ => throw new InvalidOperationException($"Unexpected result {result.GetType().Name}")
    };

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore();
        return services.BuildServiceProvider();
    }
}
