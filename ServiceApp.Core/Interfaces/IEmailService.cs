// =================================================================
//  IEmailService.cs — ServiceApp.Core/Interfaces
//
//  Contract for email sending.
//
//  WHY A SEPARATE INTERFACE?
//  Same reason as IRazorpayService — services depend on this
//  interface, not on SendGrid SDK directly. Swapping to Mailgun
//  or SMTP = change only the implementation in Infrastructure.
//
//  EMAILS WE SEND:
//  1. Request created      → customer confirmation
//  2. Technician assigned  → customer + technician notified
//  3. Job accepted         → customer (tech on the way)
//  4. Bill created         → customer (please pay)
//  5. Payment confirmed    → customer + technician receipt
// =================================================================

namespace ServiceApp.Core.Interfaces
{
    public interface IEmailService
    {
        // 1. Customer books a request
        Task SendRequestConfirmationAsync(
            string toEmail,
            string customerName,
            int requestId,
            string category,
            string preferredDateTime);

        // 2a. Customer notified — technician is assigned
        Task SendTechnicianAssignedToCustomerAsync(
            string toEmail,
            string customerName,
            int requestId,
            string technicianName,
            string technicianPhone,
            string category);

        // 2b. Technician notified — new job assigned to them
        Task SendJobAssignedToTechnicianAsync(
            string toEmail,
            string technicianName,
            int requestId,
            string customerName,
            string customerPhone,
            string address,
            string category,
            string preferredDateTime);

        // 3. Customer notified — technician accepted, on the way
        Task SendJobAcceptedToCustomerAsync(
            string toEmail,
            string customerName,
            int requestId,
            string technicianName,
            string technicianPhone);

        // 4. Customer notified — bill ready, please pay
        Task SendBillCreatedToCustomerAsync(
            string toEmail,
            string customerName,
            int requestId,
            int billId,
            decimal totalAmount);

        // 5a. Customer payment receipt
        Task SendPaymentReceiptToCustomerAsync(
            string toEmail,
            string customerName,
            int requestId,
            int billId,
            decimal amount,
            string paymentMethod,
            string transactionId,
            DateTime paidAt);

        // 5b. Technician notified — payment received, job complete
        Task SendPaymentReceiptToTechnicianAsync(
            string toEmail,
            string technicianName,
            int requestId,
            decimal amount);
    }
}