// =================================================================
//  WalletController.cs
//
//  GET  /Technician/Wallet/Index      → earnings dashboard
//  GET  /Technician/Wallet/Withdraw   → withdrawal form
//  POST /Technician/Wallet/Withdraw   → submit withdrawal
//  GET  /Technician/Wallet/Statement  → download PDF statement
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Web.Areas.Technician.Models;

namespace ServiceApp.Web.Areas.Technician.Controllers
{
    [Area("Technician")]
    [Authorize(Roles = "Technician")]
    public class WalletController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<WalletController> _logger;

        // Standard commission rate — 12%
        private const decimal CommissionRate = 0.12m;

        public WalletController(
            IUnitOfWork uow,
            UserManager<ApplicationUser> userManager,
            ILogger<WalletController> logger)
        {
            _uow = uow;
            _userManager = userManager;
            _logger = logger;
        }

        // =============================================================
        //  GET /Technician/Wallet/Index
        //  Full earnings dashboard.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;
            var tech = await _uow.TechnicianProfiles
                .GetByUserIdAsync(userId);

            if (tech == null)
            {
                TempData["Error"] = "Technician profile not found.";
                return RedirectToAction("Dashboard", "Home");
            }

            // Load all completed jobs for this technician
            var jobs = (await _uow.ServiceRequests
                .GetByTechnicianIdAsync(tech.TechnicianProfileId))
                .Where(j => j.Status == RequestStatus.Completed
                         && j.Bill != null
                         && j.Bill.PaymentStatus == PaymentStatus.Paid)
                .OrderByDescending(j => j.Bill!.PaidAt)
                .ToList();

            // Load withdrawal history
            var withdrawals = (await _uow.Withdrawals
                .GetByTechnicianAsync(userId)).ToList();

            var totalWithdrawn = withdrawals
                .Where(w => w.Status == "Paid")
                .Sum(w => w.Amount);

            var pendingWithdrawal = withdrawals
                .Where(w => w.Status == "Pending" ||
                            w.Status == "Approved")
                .Sum(w => w.Amount);

            // ── Build ViewModel ────────────────────────────────────
            var vm = new EarningsViewModel
            {
                TotalJobsCompleted = jobs.Count,
                CommissionRate = CommissionRate
            };

            // All time
            vm.GrossEarningsAllTime = jobs
                .Sum(j => j.Bill!.TotalAmount);
            vm.CommissionAllTime = Math.Round(
                vm.GrossEarningsAllTime * CommissionRate, 2);
            vm.NetEarningsAllTime =
                vm.GrossEarningsAllTime - vm.CommissionAllTime;

            vm.AveragePerJob = jobs.Any()
                ? Math.Round(vm.NetEarningsAllTime / jobs.Count, 2)
                : 0;

            // This month
            var monthStart = new DateTime(
                DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var monthJobs = jobs.Where(j =>
                j.Bill!.PaidAt >= monthStart).ToList();

            vm.GrossEarningsThisMonth =
                monthJobs.Sum(j => j.Bill!.TotalAmount);
            vm.NetEarningsThisMonth = Math.Round(
                vm.GrossEarningsThisMonth * (1 - CommissionRate), 2);

            // This week
            var weekStart = DateTime.UtcNow.Date.AddDays(
                -(int)DateTime.UtcNow.DayOfWeek);
            var weekJobs = jobs.Where(j =>
                j.Bill!.PaidAt >= weekStart).ToList();

            vm.GrossEarningsThisWeek =
                weekJobs.Sum(j => j.Bill!.TotalAmount);
            vm.NetEarningsThisWeek = Math.Round(
                vm.GrossEarningsThisWeek * (1 - CommissionRate), 2);

            // Available to withdraw
            vm.PendingWithdrawal = pendingWithdrawal;
            vm.AvailableToWithdraw = Math.Max(0,
                vm.NetEarningsAllTime
                - totalWithdrawn
                - pendingWithdrawal);

            // ── Monthly chart (last 6 months) ──────────────────────
            for (int i = 5; i >= 0; i--)
            {
                var month = DateTime.UtcNow.AddMonths(-i);
                var monthLabel = month.ToString("MMM yy");
                var gross = jobs
                    .Where(j => j.Bill!.PaidAt.HasValue
                             && j.Bill.PaidAt.Value.Year == month.Year
                             && j.Bill.PaidAt.Value.Month == month.Month)
                    .Sum(j => j.Bill!.TotalAmount);

                vm.MonthLabels.Add(monthLabel);
                vm.GrossValues.Add(gross);
                vm.NetValues.Add(Math.Round(
                    gross * (1 - CommissionRate), 2));
            }

            // ── Daily chart (last 30 days) ─────────────────────────
            for (int i = 29; i >= 0; i--)
            {
                var day = DateTime.UtcNow.Date.AddDays(-i);
                var dayLabel = day.ToString("dd MMM");
                var gross = jobs
                    .Where(j => j.Bill!.PaidAt.HasValue
                             && j.Bill.PaidAt.Value.Date == day)
                    .Sum(j => j.Bill!.TotalAmount);

                vm.DayLabels.Add(dayLabel);
                vm.DayValues.Add(gross);
            }

            // ── Payout history ─────────────────────────────────────
            vm.PayoutHistory = jobs.Select(j => new PayoutEntry
            {
                RequestId = j.RequestId,
                BillId = j.Bill!.Id,
                CustomerName = j.Customer?.FullName ?? "—",
                Category = j.Category.ToString(),
                GrossAmount = j.Bill.TotalAmount,
                Commission = Math.Round(
                    j.Bill.TotalAmount * CommissionRate, 2),
                NetAmount = Math.Round(
                    j.Bill.TotalAmount * (1 - CommissionRate), 2),
                PaymentMethod = j.Bill.Payment?.PaymentMethod ?? "—",
                PaidAt = j.Bill.PaidAt ?? j.Bill.CreatedAt
            }).ToList();

            // ── Withdrawal requests ────────────────────────────────
            vm.WithdrawalRequests = withdrawals.Select(w =>
                new Models.WithdrawalRequest
                {
                    Id = w.WithdrawalId,
                    Amount = w.Amount,
                    Status = w.Status,
                    UpiId = w.UpiId,
                    AdminNote = w.AdminNote,
                    RequestedAt = w.RequestedAt,
                    ProcessedAt = w.ProcessedAt
                }).ToList();

            return View(vm);
        }

        // =============================================================
        //  GET /Technician/Wallet/Withdraw
        //  Show withdrawal request form.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Withdraw()
        {
            var userId = _userManager.GetUserId(User)!;
            var tech = await _uow.TechnicianProfiles
                .GetByUserIdAsync(userId);

            if (tech == null)
                return RedirectToAction(nameof(Index));

            var jobs = (await _uow.ServiceRequests
                .GetByTechnicianIdAsync(tech.TechnicianProfileId))
                .Where(j => j.Status == RequestStatus.Completed
                         && j.Bill != null
                         && j.Bill.PaymentStatus == PaymentStatus.Paid)
                .ToList();

            var grossTotal = jobs.Sum(j => j.Bill!.TotalAmount);
            var netTotal = Math.Round(
                grossTotal * (1 - CommissionRate), 2);

            var withdrawn = await _uow.Withdrawals
                .GetTotalWithdrawnAsync(userId);

            var pending = (await _uow.Withdrawals
                .GetByTechnicianAsync(userId))
                .Where(w => w.Status == "Pending" ||
                            w.Status == "Approved")
                .Sum(w => w.Amount);

            var available = Math.Max(0,
                netTotal - withdrawn - pending);

            var vm = new WithdrawViewModel
            {
                AvailableAmount = available
            };

            return View(vm);
        }

        // =============================================================
        //  POST /Technician/Wallet/Withdraw
        //  Submit a withdrawal request.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(WithdrawViewModel vm)
        {
            var userId = _userManager.GetUserId(User)!;

            if (vm.RequestAmount <= 0)
            {
                ModelState.AddModelError(
                    nameof(vm.RequestAmount),
                    "Enter an amount greater than zero.");
                return View(vm);
            }

            if (vm.RequestAmount > vm.AvailableAmount)
            {
                ModelState.AddModelError(
                    nameof(vm.RequestAmount),
                    $"Maximum available: ₹{vm.AvailableAmount:N2}");
                return View(vm);
            }

            if (string.IsNullOrWhiteSpace(vm.UpiId))
            {
                ModelState.AddModelError(
                    nameof(vm.UpiId),
                    "UPI ID is required.");
                return View(vm);
            }

            if (!ModelState.IsValid) return View(vm);

            var withdrawal = new TechnicianWithdrawal
            {
                TechnicianUserId = userId,
                Amount = Math.Round(vm.RequestAmount, 2),
                Status = "Pending",
                UpiId = vm.UpiId.Trim(),
                RequestedAt = DateTime.UtcNow
            };

            await _uow.Withdrawals.AddAsync(withdrawal);
            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Withdrawal request ₹{Amount} by tech {UserId}",
                withdrawal.Amount, userId);

            TempData["Success"] =
                $"Withdrawal request of ₹{withdrawal.Amount:N2} " +
                "submitted. Admin will process within 24 hours.";

            return RedirectToAction(nameof(Index));
        }
    }
}