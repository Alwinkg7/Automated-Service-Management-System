// =================================================================
//  Program.cs — ServiceApp.Web
//
//  Application entry point. Does three things in order:
//
//  1. REGISTER services into the DI container (builder phase)
//     → EF Core, Identity, Serilog, Repositories, UnitOfWork
//
//  2. BUILD + CONFIGURE the HTTP middleware pipeline (app phase)
//     → Exception handling, HTTPS, static files, auth, routing
//
//  3. SEED initial data on first run
//     → Creates 3 roles + first admin account
//
//  HOW DI WORKS:
//  When a controller says "I need IUnitOfWork", ASP.NET looks at
//  what we registered here and injects the right concrete class.
//  We never call "new UnitOfWork()" ourselves — the framework does it.
//
//  SERVICE LIFETIMES:
//  Singleton  = one instance for the entire app lifetime
//  Scoped     = one instance per HTTP request (our default)
//  Transient  = new instance every time it's requested
// =================================================================

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Data;
using ServiceApp.Data.Context;
using ServiceApp.Infrastructure;
using ServiceApp.Services;
using ServiceApp.Services.Implementations;
using System.Text;

// ── Bootstrap Serilog BEFORE anything else ────────────────────────
// This catches startup errors (wrong connection string etc.)
// The full Serilog config (with file sink) is set up inside builder.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("ServiceApp starting up...");

    var builder = WebApplication.CreateBuilder(args);

    // =============================================================
    //  STEP 1 — Replace default .NET logging with Serilog
    //
    //  Serilog writes to:
    //    Console → visible in terminal during development
    //    File    → Logs/serviceapp-20241201.log (daily rolling)
    //
    //  Log files rotate every day, kept for 30 days.
    //  Log level from appsettings.json — easy to change per env.
    // =============================================================
    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "Logs/serviceapp-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] " +
                "{Message:lj}{NewLine}{Exception}"));

    // =============================================================
    //  STEP 2 — EF Core + SQL Server
    //
    //  Connection string lives in appsettings.json.
    //  MigrationsAssembly tells EF where migration files live —
    //  they're in ServiceApp.Data, not ServiceApp.Web.
    // =============================================================
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.MigrationsAssembly("ServiceApp.Data")));

    // =============================================================
    //  STEP 3 — ASP.NET Identity
    //
    //  Identity handles for us:
    //    Password hashing        (never store plain passwords)
    //    Login/logout sessions   (auth cookie)
    //    Account lockout         (5 wrong attempts = 5 min lock)
    //    Role management         (Admin, Technician, Customer)
    //    Token generation        (password reset links)
    //
    //  We use ApplicationUser (our extended IdentityUser)
    //  and the standard IdentityRole.
    // =============================================================
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // ── Password rules ──────────────────────────────────────
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false; // no ! # @ needed

        // ── Lockout ─────────────────────────────────────────────
        // 5 wrong passwords → locked for 5 minutes
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // ── User settings ────────────────────────────────────────
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>() // store in our SQL Server
    .AddDefaultTokenProviders();                       // for password reset etc.

    // ── JWT Authentication (for Flutter API) ──────────────────────────
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

    builder.Services.AddAuthentication(options =>
    {
        // Keep cookie auth as default for MVC (don't break existing web)
        // JWT is used only when explicitly requested
    })
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

    // ── CORS for Flutter (development) ───────────────────────────────
    //builder.Services.AddCors(options =>
    //{
    //    options.AddPolicy("FlutterDev", policy =>
    //    {
    //        policy
    //            .WithOrigins(
    //                "http://10.0.2.2",      
    //                "http://localhost:61816",
    //                "http://localhost:53274",
    //                "http://127.0.0.1"        
    //            )
    //            .AllowAnyHeader()
    //            .AllowAnyMethod()
    //            .AllowCredentials();
    //    });

    //    // Tighter policy for production — update with your real domain
    //    options.AddPolicy("FlutterProd", policy =>
    //    {
    //        policy
    //            .WithOrigins("https://serviceapp.com")
    //            .AllowAnyHeader()
    //            .WithMethods("GET", "POST", "PUT", "DELETE");
    //    });
    //});
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("FlutterDev", policy =>
        {
            policy
                .SetIsOriginAllowed(_ => true)  // ← allow ALL origins in dev
                .AllowAnyHeader()
                .AllowAnyMethod();
                //.AllowCredentials();
        });
    });

    // =============================================================
    //  STEP 4 — Auth cookie configuration
    //
    //  This controls what happens when:
    //    - Unauthenticated user hits [Authorize] page → redirect to Login
    //    - Wrong role hits [Authorize(Roles="Admin")] → AccessDenied
    //    - Cookie expires → redirect to Login
    // =============================================================
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";

        // Cookie lives for 7 days if "Remember me" is checked
        options.ExpireTimeSpan = TimeSpan.FromDays(7);

        // Sliding expiration: timer resets on each request
        // So active users never get logged out mid-session
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

    // =============================================================
    //  STEP 5 — Register our repositories + Unit of Work
    //
    //  Scoped = one instance per HTTP request.
    //  This matches EF Core's DbContext lifetime (also Scoped).
    //  Mismatching lifetimes causes "DbContext disposed" errors.
    //
    //  We register the INTERFACE → IMPLEMENTATION pair.
    //  When something asks for IUnitOfWork, it gets UnitOfWork.
    // =============================================================
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
    builder.Services.AddScoped<IBillService, BillService>(); 
    builder.Services.AddScoped<IPaymentService, PaymentService>();
    builder.Services.AddScoped<ITechnicianService, TechnicianService>();
  

    // Bind Razorpay config section to the settings class
    builder.Services.Configure<RazorpaySettings>(
        builder.Configuration.GetSection(RazorpaySettings.SectionName));

    // Register Razorpay service
    builder.Services.AddScoped<IRazorpayService, RazorpayService>();

    // =============================================================
    //  STEP 6 — MVC with Areas
    //
    //  Areas let us separate controllers and views per role:
    //    /Areas/Admin/...
    //    /Areas/Customer/...
    //    /Areas/Technician/...
    //
    //  AddControllersWithViews registers:
    //    - Controller routing
    //    - Razor view engine
    //    - Model binding
    //    - Validation
    //    - Tag helpers
    // =============================================================
    builder.Services.AddControllersWithViews();
    // ── BUILD the WebApplication ──────────────────────────────────
    var app = builder.Build();

    // =============================================================
    //  STEP 7 — Seed roles + default admin on first run
    //
    //  Creates: Admin, Technician, Customer roles
    //  Creates: Default admin account if none exists
    //  Safe to run every startup — checks before inserting.
    // =============================================================
    await SeedAsync(app);

    // =============================================================
    //  STEP 8 — HTTP Middleware Pipeline
    //
    //  ORDER IS CRITICAL — each middleware wraps the next.
    //  Wrong order = auth bypass, broken routing, hidden errors.
    //
    //  Correct order:
    //  1. Exception handling   (outermost — catches everything)
    //  2. HTTPS redirect
    //  3. Static files         (CSS, JS — no auth needed)
    //  4. Serilog request log  (log before routing decision)
    //  5. Routing              (match URL to endpoint)
    //  6. Authentication       (read cookie → set User.Identity)
    //  7. Authorization        (check [Authorize] attributes)
    //  8. Endpoints            (invoke the controller action)
    // =============================================================

   

    if (!app.Environment.IsDevelopment())
    {
        // Production: show a friendly error page instead of stack trace
        app.UseExceptionHandler("/Home/Error");
        // Tell browsers to use HTTPS for next 365 days (HSTS)
        app.UseHsts();
    }

    //app.UseHttpsRedirection();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();   // only force HTTPS in production
    }

    // Serve files from wwwroot (CSS, JS, images) — no auth check
    app.UseStaticFiles();

    // Logs every HTTP request: "GET /Customer/Dashboard → 200 in 45ms"
    // Place AFTER static files so asset requests don't pollute logs
    app.UseSerilogRequestLogging();

    app.UseRouting();
    var isDev = app.Environment.IsDevelopment();
    //app.UseCors(isDev ? "FlutterDev" : "FlutterProd");
    app.UseCors("FlutterDev");
    // Authentication MUST come before Authorization
    // UseAuthentication reads the cookie and sets User.Identity
    // UseAuthorization checks the [Authorize] attributes
    app.UseAuthentication();
    app.UseAuthorization();

    // =============================================================
    //  STEP 9 — Route configuration
    //
    //  Area route MUST be registered BEFORE the default route.
    //  Otherwise /Admin/Home/Dashboard would try to match the
    //  default route first and fail.
    //
    //  Area route:    /Admin/Requests/Index
    //  Default route: /Account/Login
    // =============================================================
    app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Dashboard}/{id?}");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
// AFTER
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is thrown intentionally by EF Tools
    // after reading the DbContext for migrations — not a real crash.
    // All other exceptions ARE real startup failures.
    Log.Fatal(ex, "ServiceApp failed to start.");
}
finally
{
    // Always flush and close the log file cleanly on shutdown
    Log.CloseAndFlush();
}


// =================================================================
//  DATA SEEDER
//
//  Runs once at every startup but only INSERTS if missing.
//  Creates the three Identity roles and the first admin account.
//
//  First admin credentials (change password after first login!):
//    Email:    admin@serviceapp.com
//    Password: Admin@1234
//
//  To add more admins later:
//    → Admin panel → Create User → Role = Admin
//    (we build this in Phase 2)
// =================================================================
static async Task SeedAsync(WebApplication app)
{
    // CreateScope: get a DI scope outside the request pipeline
    using var scope = app.Services.CreateScope();

    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<ApplicationUser>>();

    // ── Create the 3 roles ────────────────────────────────────────
    // These map to [Authorize(Roles = "Admin")] etc. in controllers
    string[] roles = ["Admin", "Technician", "Customer"];
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
            Log.Information("Role created: {Role}", role);
        }
    }

    // ── Create the default admin account ─────────────────────────
    // Only created if no admin exists yet.
    const string adminEmail = "admin@serviceapp.com";
    const string adminPassword = "Admin@1234";

    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "System Admin",
            Phone = "0000000000",
            Role = UserRole.Admin,
            EmailConfirmed = true,  // skip email confirmation for seeded admin
            IsActive = true
        };

        var result = await userManager.CreateAsync(admin, adminPassword);

        if (result.Succeeded)
        {
            // Add to Identity roles table (what [Authorize] checks)
            await userManager.AddToRoleAsync(admin, "Admin");
            Log.Information(
                "Default admin seeded: {Email}", adminEmail);
        }
        else
        {
            foreach (var err in result.Errors)
                Log.Error("Admin seed failed: {Error}", err.Description);
        }
    }
}