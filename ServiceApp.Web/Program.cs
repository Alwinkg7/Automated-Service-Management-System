using ServiceApp.Hosting.DependencyInjection;
using ServiceApp.Hosting.Logging;
using ServiceApp.Hosting.Realtime;
using ServiceApp.Hosting.Seeding;
using Serilog;

// ServiceApp.Web — MVC host (cookie auth, Areas Admin/Customer/Technician).
// Business logic comes from the shared composition in ServiceApp.Hosting.
// This host enqueues Hangfire jobs but never processes them (the API host does).
await SerilogHostExtensions.RunServiceAppHostAsync("ServiceApp.Web", async () =>
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddServiceAppSerilog("serviceapp");

    builder.Services.AddServiceAppCore(builder.Configuration);
    builder.Services.AddServiceAppRealtime(builder.Configuration);
    builder.Services.AddServiceAppHangfireStorage(builder.Configuration);

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

    builder.Services.AddControllersWithViews();

    var app = builder.Build();

    StartupDiagnostics.WarnOnMissingSecrets(app.Configuration, app.Logger);
    await app.Services.InitializeDatabaseAsync(app.Configuration);

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseStaticFiles();
    app.UseSerilogRequestLogging();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Dashboard}/{id?}");
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.MapHub<ServiceHub>("/hubs/service");
    app.MapHealthChecks("/health");

    await app.RunAsync();
});
