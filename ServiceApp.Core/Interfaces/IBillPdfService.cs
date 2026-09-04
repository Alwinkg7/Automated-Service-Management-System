// =================================================================
//  IBillPdfService.cs — ServiceApp.Core/Interfaces
//
//  Contract for PDF bill generation.
//
//  WHY A SEPARATE SERVICE?
//  PDF generation is a pure output concern — it takes data
//  and produces bytes. It has no business logic, no DB writes.
//  Keeping it separate means BillService stays clean and this
//  can be swapped (QuestPDF → iText → SSRS) without touching
//  anything else.
//
//  FLOW:
//  1. Customer or technician clicks "Download bill"
//  2. Controller calls GenerateBillPdfAsync(billId)
//  3. Service loads the bill with all related data
//  4. QuestPDF renders it to a byte array
//  5. Controller returns File(bytes, "application/pdf", "bill.pdf")
//  6. Browser downloads the PDF
// =================================================================

namespace ServiceApp.Core.Interfaces
{
    public interface IBillPdfService
    {
        // Generate a PDF for a specific bill.
        // Returns the PDF as a byte array — controller streams it
        // to the browser as a file download.
        // Throws if the bill is not found.
        Task<byte[]> GenerateBillPdfAsync(int billId);
    }
}