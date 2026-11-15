using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace ServiceApp.Hosting.Logging;

/// <summary>Serilog bootstrap shared by both hosts.</summary>
public static class SerilogHostExtensions
{
    /// <summary>Console-only logger that captures startup failures before configuration is loaded.</summary>
    public static void CreateBootstrapLogger() =>
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();

    /// <summary>
    /// Replaces default logging with Serilog: levels from the "Serilog" config
    /// section, console sink and a daily rolling file under {ContentRoot}/Logs.
    /// </summary>
    public static WebApplicationBuilder AddServiceAppSerilog(this WebApplicationBuilder builder, string logFilePrefix)
    {
        var logPath = Path.Combine(builder.Environment.ContentRootPath, "Logs", $"{logFilePrefix}-.log");

        builder.Host.UseSerilog((ctx, services, config) => config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", ctx.HostingEnvironment.ApplicationName)
            .WriteTo.Console()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

        return builder;
    }

    /// <summary>
    /// Runs the host inside the standard try/catch/flush envelope. EF tooling's
    /// <see cref="HostAbortedException"/> is not treated as a crash.
    /// </summary>
    public static async Task RunServiceAppHostAsync(string appName, Func<Task> run)
    {
        CreateBootstrapLogger();
        try
        {
            Log.Information("{App} starting up...", appName);
            await run();
        }
        catch (Exception ex) when (ex is not HostAbortedException)
        {
            Log.Fatal(ex, "{App} failed to start.", appName);
            throw;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
