using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Web.Areas.Admin.Controllers
{
    /// <summary>Admin processing of technician withdrawals (Pending → Approved → Paid, or Rejected).</summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class WithdrawalsController : Controller
    {
        private readonly IWalletService _wallet;
        private readonly UserManager<ApplicationUser> _userManager;

        public WithdrawalsController(IWalletService wallet, UserManager<ApplicationUser> userManager)
        {
            _wallet = wallet;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index() =>
            View((await _wallet.GetOpenWithdrawalsAsync()).ToList());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id) =>
            Done(await _wallet.ApproveAsync(id, AdminId), w => $"Withdrawal #{id} approved. Process payment to {w.UpiId}.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id) =>
            Done(await _wallet.MarkPaidAsync(id, AdminId), w => $"₹{w.Amount:N2} marked as paid to {w.UpiId}.");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? adminNote) =>
            Done(await _wallet.RejectAsync(id, AdminId, adminNote), _ => $"Withdrawal #{id} rejected.");

        private string AdminId => _userManager.GetUserId(User)!;

        private IActionResult Done(Result<TechnicianWithdrawal> result, Func<TechnicianWithdrawal, string> success)
        {
            if (result.IsSuccess)
                TempData["Success"] = success(result.Data!);
            else
                TempData["Error"] = result.ErrorMessage;

            return RedirectToAction(nameof(Index));
        }
    }
}
