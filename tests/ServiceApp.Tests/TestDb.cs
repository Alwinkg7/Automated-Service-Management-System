using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Data;
using ServiceApp.Data.Context;

namespace ServiceApp.Tests;

/// <summary>Isolated in-memory database + UnitOfWork + Identity for service-level regression tests.</summary>
public sealed class TestDb : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;

    public ApplicationDbContext Context { get; }
    public UnitOfWork Uow { get; }
    public UserManager<ApplicationUser> Users { get; }

    public TestDb()
    {
        var name = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequireNonAlphanumeric = false;
                o.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        Context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Uow = new UnitOfWork(Context);

        var roles = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Technician", "Customer" })
            roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
    }

    public static NullLogger<T> Log<T>() => NullLogger<T>.Instance;

    /// <summary>Seeds a customer, a technician and a Billed request with an unpaid bill.</summary>
    public async Task<Scenario> SeedBilledJobAsync(decimal amount = 850m, string? razorpayOrderId = null)
    {
        var customer = new ApplicationUser { Id = "cust-1", UserName = "c@x.com", Email = "c@x.com", FullName = "Cust", Role = UserRole.Customer };
        var techUser = new ApplicationUser { Id = "tech-1", UserName = "t@x.com", Email = "t@x.com", FullName = "Tech", Role = UserRole.Technician };
        var otherTechUser = new ApplicationUser { Id = "tech-2", UserName = "t2@x.com", Email = "t2@x.com", FullName = "Other", Role = UserRole.Technician };
        Context.Users.AddRange(customer, techUser, otherTechUser);

        var tech = new TechnicianProfile { UserId = techUser.Id, Skill = ServiceCategory.Plumbing, Status = TechnicianStatus.Busy };
        var otherTech = new TechnicianProfile { UserId = otherTechUser.Id, Skill = ServiceCategory.Plumbing };
        Context.TechnicianProfiles.AddRange(tech, otherTech);
        await Context.SaveChangesAsync();

        var request = new ServiceRequest
        {
            CustomerId = customer.Id,
            AssignedTechnicianProfileId = tech.TechnicianProfileId,
            Category = ServiceCategory.Plumbing,
            Status = RequestStatus.Billed,
            Description = "Leaking tap",
            Address = "12 MG Road",
            PreferredDateTime = DateTime.UtcNow.AddDays(1)
        };
        Context.ServiceRequests.Add(request);
        await Context.SaveChangesAsync();

        var bill = new Bill
        {
            ServiceRequestId = request.RequestId,
            TechnicianProfileId = tech.TechnicianProfileId,
            TotalAmount = amount,
            PaymentStatus = PaymentStatus.Unpaid,
            RazorpayOrderId = razorpayOrderId
        };
        Context.Bills.Add(bill);
        await Context.SaveChangesAsync();

        return new Scenario(customer.Id, techUser.Id, otherTechUser.Id, request.RequestId, bill.Id);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}

public record Scenario(string CustomerId, string TechUserId, string OtherTechUserId, int RequestId, int BillId);
