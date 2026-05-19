// =================================================================
//  ApplicationDbContext.cs — ServiceApp.Data/Context
//
//  EF Core's "database session". Everything flows through here.
//
//  Inherits from IdentityDbContext<ApplicationUser> which
//  automatically creates these Identity tables for us:
//    AspNetUsers         ← our ApplicationUser maps here
//    AspNetRoles         ← "Admin", "Technician", "Customer"
//    AspNetUserRoles     ← which user has which role
//    AspNetUserClaims, AspNetUserLogins, AspNetUserTokens
//
//  On top of those, we add our own 7 tables:
//    CustomerProfiles
//    TechnicianProfiles
//    AdminProfiles
//    ServiceRequests
//    ServiceHistories
//    Bills
//    BillItems
//    Payments
//
//  HOW TO USE:
//  Never use this class directly in controllers or services.
//  Always go through the Repository → UnitOfWork chain.
//  DbContext is registered as Scoped (one per HTTP request).
// =================================================================

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceApp.Core.Entities;

namespace ServiceApp.Data.Context
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        // Options injected by DI — carries the connection string
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // ── DbSet = SQL Table ──────────────────────────────────────
        // Property name becomes the table name in the database.
        // EF Core uses these to know which types it manages.
        public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
        public DbSet<TechnicianProfile> TechnicianProfiles => Set<TechnicianProfile>();
        public DbSet<AdminProfile> AdminProfiles => Set<AdminProfile>();
        public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
        public DbSet<ServiceHistory> ServiceHistories => Set<ServiceHistory>();
        public DbSet<Bill> Bills => Set<Bill>();
        public DbSet<BillItem> BillItems => Set<BillItem>();
        public DbSet<Payment> Payments => Set<Payment>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // CRITICAL — must call base first.
            // This sets up ALL the Identity tables.
            // Skipping this breaks login completely.
            base.OnModelCreating(builder);

            ConfigureApplicationUser(builder);
            ConfigureCustomerProfile(builder);
            ConfigureTechnicianProfile(builder);
            ConfigureAdminProfile(builder);
            ConfigureServiceRequest(builder);
            ConfigureServiceHistory(builder);
            ConfigureBill(builder);
            ConfigureBillItem(builder);
            ConfigurePayment(builder);
        }

        // =============================================================
        //  ApplicationUser
        //  Table: AspNetUsers (managed by Identity — we just ADD columns)
        // =============================================================
        private static void ConfigureApplicationUser(ModelBuilder builder)
        {
            builder.Entity<ApplicationUser>(e =>
            {
                // Limit column sizes — avoids nvarchar(MAX) everywhere
                e.Property(u => u.FullName)
                    .HasMaxLength(100)
                    .IsRequired();

                e.Property(u => u.Phone)
                    .HasMaxLength(15);

                // Store enum as string: "Customer" not 0
                // Makes the DB readable without a lookup table
                e.Property(u => u.Role)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();

                // Index on Role — admin filtering queries use this heavily
                // e.g. "get all technicians" = WHERE Role = 'Technician'
                e.HasIndex(u => u.Role);
            });
        }

        // =============================================================
        //  CustomerProfile
        //  One-to-one with ApplicationUser
        //  A customer may or may not have completed their profile yet.
        // =============================================================
        private static void ConfigureCustomerProfile(ModelBuilder builder)
        {
            builder.Entity<CustomerProfile>(e =>
            {
                e.HasKey(c => c.CustomerProfileId);

                e.Property(c => c.Address).HasMaxLength(300);
                e.Property(c => c.City).HasMaxLength(100);
                e.Property(c => c.PinCode).HasMaxLength(10);
                e.Property(c => c.AvatarUrl).HasMaxLength(500);

                // Store enum as string — nullable (customer hasn't set it yet)
                e.Property(c => c.PreferredCategory)
                    .HasConversion<string>()
                    .HasMaxLength(50);

                // One-to-one: ApplicationUser ↔ CustomerProfile
                // If the user is deleted → their profile is also deleted
                e.HasOne(c => c.User)
                    .WithOne(u => u.CustomerProfile)
                    .HasForeignKey<CustomerProfile>(c => c.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique: one user → at most one customer profile row
                e.HasIndex(c => c.UserId).IsUnique();
            });
        }

        // =============================================================
        //  TechnicianProfile
        //  One-to-one with ApplicationUser
        //  Most data-rich profile — used by assignment engine
        // =============================================================
        private static void ConfigureTechnicianProfile(ModelBuilder builder)
        {
            builder.Entity<TechnicianProfile>(e =>
            {
                e.HasKey(t => t.TechnicianProfileId);

                // Skill and Status stored as strings for readability
                e.Property(t => t.Skill)
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .IsRequired();

                e.Property(t => t.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();

                // decimal(3,2) → max value 9.99
                // Suitable for star ratings: 0.00 to 5.00
                e.Property(t => t.Rating)
                    .HasColumnType("decimal(3,2)");

                e.Property(t => t.Bio).HasMaxLength(500);
                e.Property(t => t.ServiceAreaPinCode).HasMaxLength(10);
                e.Property(t => t.AvatarUrl).HasMaxLength(500);

                // One-to-one with ApplicationUser
                e.HasOne(t => t.User)
                    .WithOne(u => u.TechnicianProfile)
                    .HasForeignKey<TechnicianProfile>(t => t.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Fast lookup: "find this user's technician profile"
                e.HasIndex(t => t.UserId).IsUnique();

                // AUTO-ASSIGNMENT INDEXES
                // The assignment engine queries: WHERE Status='Available' AND Skill='Plumber'
                // These two indexes make that query instant even with thousands of technicians
                e.HasIndex(t => t.Status);
                e.HasIndex(t => t.Skill);
            });
        }

        // =============================================================
        //  AdminProfile
        //  One-to-one with ApplicationUser
        //  Minimal — admin is internal staff
        // =============================================================
        private static void ConfigureAdminProfile(ModelBuilder builder)
        {
            builder.Entity<AdminProfile>(e =>
            {
                e.HasKey(a => a.AdminProfileId);

                e.Property(a => a.Department).HasMaxLength(100);
                e.Property(a => a.Designation).HasMaxLength(100);
                e.Property(a => a.AvatarUrl).HasMaxLength(500);
                e.Property(a => a.EmployeeId).HasMaxLength(50);

                e.HasOne(a => a.User)
                    .WithOne(u => u.AdminProfile)
                    .HasForeignKey<AdminProfile>(a => a.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(a => a.UserId).IsUnique();
            });
        }

        // =============================================================
        //  ServiceRequest
        //  The core entity — the "ride" of this platform
        // =============================================================
        private static void ConfigureServiceRequest(ModelBuilder builder)
        {
            builder.Entity<ServiceRequest>(e =>
            {
                e.HasKey(r => r.RequestId);

                e.Property(r => r.Description)
                    .HasMaxLength(1000)
                    .IsRequired();

                e.Property(r => r.Category)
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .IsRequired();

                e.Property(r => r.Status)
                    .HasConversion<string>()
                    .HasMaxLength(30)
                    .IsRequired();

                e.Property(r => r.Address).HasMaxLength(300);
                e.Property(r => r.PinCode).HasMaxLength(10);
                e.Property(r => r.CustomerFeedback).HasMaxLength(500);

                // Many requests → one customer
                // Restrict: can't delete a user who has requests
                e.HasOne(r => r.Customer)
                    .WithMany(u => u.ServiceRequests)
                    .HasForeignKey(r => r.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Many requests → one technician profile (nullable until assigned)
                // SetNull: if technician profile deleted, remove the assignment
                // but keep the request
                e.HasOne(r => r.AssignedTechnician)
                    .WithMany(t => t.AssignedRequests)
                    .HasForeignKey(r => r.AssignedTechnicianProfileId)
                    .OnDelete(DeleteBehavior.SetNull);

                // ── PERFORMANCE INDEXES ────────────────────────────
                // Every common WHERE clause should have an index.

                // Admin "All Pending requests" view
                e.HasIndex(r => r.Status);

                // Customer "My requests" view
                e.HasIndex(r => r.CustomerId);

                // Technician "My jobs" view
                e.HasIndex(r => r.AssignedTechnicianProfileId);

                // Auto-assignment: oldest pending request first
                e.HasIndex(r => r.CreatedAt);

                // Composite: status + created — used by auto-assignment
                // "Give me Pending requests ordered by date"
                e.HasIndex(r => new { r.Status, r.CreatedAt });
            });
        }

        // =============================================================
        //  ServiceHistory
        //  Append-only audit log — one row per status change
        // =============================================================
        private static void ConfigureServiceHistory(ModelBuilder builder)
        {
            builder.Entity<ServiceHistory>(e =>
            {
                e.HasKey(h => h.HistoryId);

                e.Property(h => h.Status)
                    .HasConversion<string>()
                    .HasMaxLength(30)
                    .IsRequired();

                e.Property(h => h.Note).HasMaxLength(500);

                // ChangedByUserId can be "SYSTEM" or a user's GUID
                e.Property(h => h.ChangedByUserId)
                    .HasMaxLength(450)
                    .IsRequired();

                // When the request is deleted, its history goes too (cascade)
                e.HasOne(h => h.Request)
                    .WithMany(r => r.History)
                    .HasForeignKey(h => h.RequestId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Loading history for a request = WHERE RequestId = X
                e.HasIndex(h => h.RequestId);
            });
        }

        // =============================================================
        //  Bill
        //  Invoice header — one bill per request
        // =============================================================
        private static void ConfigureBill(ModelBuilder builder)
        {
            builder.Entity<Bill>(e =>
            {
                e.HasKey(b => b.BillId);

                // decimal(10,2) handles up to ₹99,999,999.99
                e.Property(b => b.TotalAmount)
                    .HasColumnType("decimal(10,2)")
                    .IsRequired();

                e.Property(b => b.PaymentStatus)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();

                // One-to-one: one request → one bill
                // Restrict: don't delete a request that has a bill
                e.HasOne(b => b.Request)
                    .WithOne(r => r.Bill)
                    .HasForeignKey<Bill>(b => b.RequestId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Bill linked to the technician who created it
                e.HasOne(b => b.Technician)
                    .WithMany(t => t.Bills)
                    .HasForeignKey(b => b.TechnicianProfileId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Enforce one bill per request at the DB level
                e.HasIndex(b => b.RequestId).IsUnique();

                // Admin "Unpaid bills" report
                e.HasIndex(b => b.PaymentStatus);
            });
        }

        // =============================================================
        //  BillItem
        //  Line items inside a bill
        // =============================================================
        private static void ConfigureBillItem(ModelBuilder builder)
        {
            builder.Entity<BillItem>(e =>
            {
                e.HasKey(i => i.BillItemId);

                e.Property(i => i.Description)
                    .HasMaxLength(300)
                    .IsRequired();

                e.Property(i => i.UnitPrice)
                    .HasColumnType("decimal(10,2)")
                    .IsRequired();

                // Amount = Quantity × UnitPrice — computed, not stored
                // EF Core is told to IGNORE this property completely
                e.Ignore(i => i.Amount);

                // Cascade: delete items when their bill is deleted
                e.HasOne(i => i.Bill)
                    .WithMany(b => b.BillItems)
                    .HasForeignKey(i => i.BillId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        // =============================================================
        //  Payment
        //  Created after gateway webhook confirms payment
        // =============================================================
        private static void ConfigurePayment(ModelBuilder builder)
        {
            builder.Entity<Payment>(e =>
            {
                e.HasKey(p => p.PaymentId);

                e.Property(p => p.Amount)
                    .HasColumnType("decimal(10,2)")
                    .IsRequired();

                e.Property(p => p.PaymentMethod)
                    .HasMaxLength(50)
                    .IsRequired();

                e.Property(p => p.GatewayTransactionId).HasMaxLength(200);
                e.Property(p => p.GatewayOrderId).HasMaxLength(200);

                // One-to-one: one bill → one payment
                e.HasOne(p => p.Bill)
                    .WithOne(b => b.Payment)
                    .HasForeignKey<Payment>(p => p.BillId)
                    .OnDelete(DeleteBehavior.Restrict);

                // UNIQUE on GatewayTransactionId — idempotency guarantee.
                // If Razorpay fires the webhook twice for the same payment,
                // the second INSERT fails here with a unique constraint error.
                // We catch that and return 200 OK (already processed).
                e.HasIndex(p => p.GatewayTransactionId).IsUnique();
            });
        }
    }
}