using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;

namespace ServiceApp.Api.Infrastructure;

/// <summary>
/// Admin-only Hangfire dashboard. Accepts either an Admin JWT (Authorization
/// header) or the Identity cookie issued by the MVC host — both hosts share the
/// Data Protection application name "ASMS", so an admin logged into the MVC site
/// on the same domain can open /hangfire on the API.
/// </summary>
public sealed class HangfireAdminAuthorizationFilter : IDashboardAsyncAuthorizationFilter
{
    public async Task<bool> AuthorizeAsync(DashboardContext context)
    {
        var http = context.GetHttpContext();

        foreach (var scheme in new[] { JwtBearerDefaults.AuthenticationScheme, IdentityConstants.ApplicationScheme })
        {
            var result = await http.AuthenticateAsync(scheme);
            if (result.Succeeded && result.Principal.IsInRole("Admin"))
                return true;
        }

        return false;
    }
}
