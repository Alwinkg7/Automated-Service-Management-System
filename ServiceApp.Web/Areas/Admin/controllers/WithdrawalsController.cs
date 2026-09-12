// =================================================================
//  WithdrawalsController.cs (Admin area)
//
//  GET  /Admin/Withdrawals/Index     → all pending withdrawals
//  POST /Admin/Withdrawals/Approve   → approve + mark for payout
//  POST /Admin/Withdrawals/Pay       → mark as paid
//  POST /Admin/Withdrawals/Reject    → reject with note
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class WithdrawalsController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<WithdrawalsController> _logger;

        public WithdrawalsController(
            IUnitOfWork uow,
            ILogger<WithdrawalsController> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var pending = (await _uow.Withdrawals
                .GetAllPendingAsync()).ToList();
            return View(pending);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var w = await _uow.Withdrawals.GetByIdAsync(id);
            if (w == null)
            {
                TempData["Error"] = "Withdrawal not found.";
                return RedirectToAction(nameof(Index));
            }

            w.Status = "Approved";
            _uow.Withdrawals.Update(w);
            await _uow.SaveChangesAsync();

            TempData["Success"] =
                $"Withdrawal #{id} approved. Process payment to {w.UpiId}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id)
        {
            var w = await _uow.Withdrawals.GetByIdAsync(id);
            if (w == null)
            {
                TempData["Error"] = "Withdrawal not found.";
                return RedirectToAction(nameof(Index));
            }

            w.Status = "Paid";
            w.ProcessedAt = DateTime.UtcNow;
            _uow.Withdrawals.Update(w);
            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Withdrawal #{Id} ₹{Amount} marked Paid by admin {Admin}",
                id, w.Amount, User.Identity?.Name);

            TempData["Success"] =
                $"₹{w.Amount:N2} marked as paid to {w.UpiId}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string adminNote)
        {
            var w = await _uow.Withdrawals.GetByIdAsync(id);
            if (w == null)
            {
                TempData["Error"] = "Withdrawal not found.";
                return RedirectToAction(nameof(Index));
            }

            w.Status = "Rejected";
            w.AdminNote = adminNote?.Trim();
            w.ProcessedAt = DateTime.UtcNow;
            _uow.Withdrawals.Update(w);
            await _uow.SaveChangesAsync();

            TempData["Success"] =
                $"Withdrawal #{id} rejected.";
            return RedirectToAction(nameof(Index));
        }
    }
}