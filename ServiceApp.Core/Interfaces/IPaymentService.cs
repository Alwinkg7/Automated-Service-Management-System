using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;

public interface IPaymentService
{
    Task<Result<Payment>> ProcessCashPaymentAsync(
        int billId, string customerUserId);

    Task<Result<Bill>> GetBillForPaymentAsync(
        int requestId, string customerUserId);

    // Add this — used by Confirmation page
    Task<Payment?> GetPaymentByIdAsync(int paymentId);
}