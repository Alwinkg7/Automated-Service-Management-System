using Hangfire;
using ServiceApp.Api.Extensions;
using ServiceApp.Api.Infrastructure;
using ServiceApp.Hosting.DependencyInjection;
using ServiceApp.Hosting.Logging;
using ServiceApp.Hosting.Realtime;
using ServiceApp.Hosting.Seeding;
using Serilog;

// ServiceApp.Api — REST API (JWT) for the Next.js and Flutter clients.
// The only host that runs the Hangfire server and recurring jobs.
await SerilogHostExtensions.RunServiceAppHostAsync("ServiceApp.Api", async () =>
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddServiceAppSerilog("serviceapp-api");

    builder.Services.AddServiceAppCore(builder.Configuration);
    builder.Services.AddServiceAppRealtime(builder.Configuration);
    builder.Services.AddServiceAppHangfireStorage(builder.Configuration);
    builder.Services.AddApiHangfireServer(builder.Configuration);

    builder.Services.AddApiAuthentication(builder.Configuration);
    builder.Services.AddApiCors(builder.Configuration, builder.Environment);
    builder.Services.AddApiRateLimiting();
    builder.Services.AddApiProblemDetails();
    builder.Services.AddApiControllers();
    if (builder.Environment.IsDevelopment())
        builder.Services.AddApiSwagger();

    var app = builder.Build();

    StartupDiagnostics.WarnOnMissingSecrets(app.Configuration, app.Logger, "Jwt:Key");
    await app.Services.InitializeDatabaseAsync(app.Configuration);

    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    else
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseSerilogRequestLogging();
    app.UseRouting();
    app.UseCors(ApiServiceCollectionExtensions.CorsPolicy);
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [],
        AsyncAuthorization = [new HangfireAdminAuthorizationFilter()]
    });
    RecurringJobs.Register(app.Services, app.Logger);

    app.MapControllers();
    app.MapHub<ServiceHub>("/hubs/service");
    app.MapHealthChecks("/health");

    await app.RunAsync();
});
