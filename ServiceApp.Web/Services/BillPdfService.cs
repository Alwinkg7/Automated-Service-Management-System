// =================================================================
//  BillPdfService.cs — ServiceApp.Web/Services
//
//  Generates a professional PDF invoice using QuestPDF.
//
//  QUESTPDF CONCEPTS:
//  Document → Page → Column → Row → Text/Table
//
//  Everything is a "container" with fluent API:
//  container.Row(row => {
//      row.RelativeItem().Text("Hello");
//      row.ConstantItem(100).Text("World");
//  });
//
//  Sizes are in points (1 point = 1/72 inch).
//  A4 = 595 × 842 points.
//
//  LICENCE:
//  QuestPDF Community licence is free for open-source and
//  revenue < $1M USD. Set it in Program.cs:
//  QuestPDF.Settings.License = LicenseType.Community;
// =================================================================

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Web.Services
{
    public class BillPdfService : IBillPdfService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<BillPdfService> _logger;

        public BillPdfService(
            IUnitOfWork uow,
            ILogger<BillPdfService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        public async Task<byte[]> GenerateBillPdfAsync(int billId)
        {
            // Load bill with all related data needed for the PDF
            var bill = await _uow.Bills
                .GetWithItemsAndPaymentAsync(billId);

            if (bill == null)
                throw new InvalidOperationException(
                    $"Bill #{billId} not found.");

            _logger.LogInformation(
                "Generating PDF for bill #{BillId}", billId);

            // Generate PDF and return as byte array
            var document = new BillDocument(bill);
            return document.GeneratePdf();
        }
    }

    // =================================================================
    //  BillDocument — the QuestPDF document definition
    //
    //  Implements IDocument — QuestPDF calls Compose() to build it.
    //  All layout and styling lives here.
    //
    //  PAGE LAYOUT:
    //  ┌─────────────────────────────────┐
    //  │  Header: Logo | Invoice title   │
    //  ├─────────────────────────────────┤
    //  │  From (technician) | To (cust.) │
    //  ├─────────────────────────────────┤
    //  │  Bill details strip             │
    //  ├─────────────────────────────────┤
    //  │  Items table (description, qty, │
    //  │  unit price, amount)            │
    //  ├─────────────────────────────────┤
    //  │  Total row                      │
    //  ├─────────────────────────────────┤
    //  │  Payment status                 │
    //  ├─────────────────────────────────┤
    //  │  Footer: thank you note         │
    //  └─────────────────────────────────┘
    // =================================================================
    internal class BillDocument : IDocument
    {
        private readonly ServiceApp.Core.Entities.Bill _bill;

        // Brand colors (use Color instead of string)
        private static readonly Color PrimaryColor = Color.FromHex("#4F46E5");  // indigo
        private static readonly Color LightColor = Color.FromHex("#EEF2FF");  // light indigo
        private static readonly Color TextMuted = Color.FromHex("#6B7280");  // gray
        private static readonly Color BorderColor = Color.FromHex("#E5E7EB");  // light border
        private static readonly Color SuccessColor = Color.FromHex("#059669");  // green
        private static readonly Color DangerColor = Color.FromHex("#DC2626");  // red

        public BillDocument(ServiceApp.Core.Entities.Bill bill)
        {
            _bill = bill;
        }

        public DocumentMetadata GetMetadata() => new DocumentMetadata
        {
            Title = $"ServiceApp Invoice — Bill #{_bill.Id}",
            Author = "ServiceApp",
            Subject = $"Invoice for Request #{_bill.ServiceRequestId}",
            Creator = "ServiceApp PDF Engine"
        };

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
        }

        // =============================================================
        //  HEADER — ServiceApp brand + invoice title
        // =============================================================
        void ComposeHeader(IContainer container)
        {
            container.PaddingBottom(20).Row(row =>
            {
                // Left: brand name + tagline
                row.RelativeItem().Column(col =>
                {
                    col.Item()
                        .Text("ServiceApp")
                        .FontSize(22)
                        .FontColor(PrimaryColor)
                        .Bold();

                    col.Item()
                        .Text("Your trusted service platform")
                        .FontSize(10)
                        .FontColor(TextMuted);
                });

                // Right: Invoice label + bill number
                row.ConstantItem(180).Column(col =>
                {
                    col.Item()
                        .AlignRight()
                        .Text("INVOICE")
                        .FontSize(20)
                        .FontColor(PrimaryColor)
                        .Bold();

                    col.Item()
                        .AlignRight()
                        .Text($"#{_bill.Id}")
                        .FontSize(13)
                        .FontColor(TextMuted);
                });
            });
        }

        // =============================================================
        //  CONTENT — main body of the invoice
        // =============================================================
        void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                // Divider line after header
                col.Item().LineHorizontal(0.5f).LineColor(BorderColor);
                col.Item().PaddingVertical(16).Element(ComposeParties);

                col.Item().LineHorizontal(0.5f).LineColor(BorderColor);
                col.Item().PaddingVertical(14).Element(ComposeBillDetails);

                col.Item().LineHorizontal(0.5f).LineColor(BorderColor);
                col.Item().PaddingTop(16).Element(ComposeItemsTable);

                col.Item().PaddingTop(14).Element(ComposeTotal);
                col.Item().PaddingTop(14).Element(ComposePaymentStatus);

                // Extra info if description present
                if (!string.IsNullOrEmpty(_bill.Description))
                {
                    col.Item().PaddingTop(14).Element(ComposeNotes);
                }
            });
        }

        // =============================================================
        //  PARTIES — From (technician) and To (customer)
        // =============================================================
        void ComposeParties(IContainer container)
        {
            container.Row(row =>
            {
                // FROM: technician
                row.RelativeItem().Column(col =>
                {
                    col.Item()
                        .Text("From")
                        .FontSize(10)
                        .FontColor(TextMuted)
                        .Bold();

                    col.Item().PaddingTop(4)
                        .Text(_bill.Technician?.User?.FullName ?? "Technician")
                        .FontSize(14)
                        .Bold();

                    col.Item()
                        .Text(_bill.Technician?.Skill.ToString() ?? "")
                        .FontSize(11)
                        .FontColor(TextMuted);

                    col.Item()
                        .Text(_bill.Technician?.User?.Phone ?? "")
                        .FontSize(11)
                        .FontColor(TextMuted);

                    col.Item()
                        .Text(_bill.Technician?.User?.Email ?? "")
                        .FontSize(11)
                        .FontColor(TextMuted);
                });

                // TO: customer
                row.ConstantItem(220).Column(col =>
                {
                    col.Item()
                        .Text("To")
                        .FontSize(10)
                        .FontColor(TextMuted)
                        .Bold();

                    var customer = _bill.ServiceRequest?.Customer;

                    col.Item().PaddingTop(4)
                        .Text(customer?.FullName ?? "Customer")
                        .FontSize(14)
                        .Bold();

                    col.Item()
                        .Text(customer?.Phone ?? "")
                        .FontSize(11)
                        .FontColor(TextMuted);

                    col.Item()
                        .Text(customer?.Email ?? "")
                        .FontSize(11)
                        .FontColor(TextMuted);

                    if (!string.IsNullOrEmpty(
                        _bill.ServiceRequest?.Address))
                    {
                        col.Item()
                            .Text(_bill.ServiceRequest.Address)
                            .FontSize(11)
                            .FontColor(TextMuted);
                    }
                });
            });
        }

        // =============================================================
        //  BILL DETAILS STRIP — dates, IDs, service type
        // =============================================================
        void ComposeBillDetails(IContainer container)
        {
            container
                .Background(LightColor)
                .Padding(12)
                .Row(row =>
                {
                    DetailCell(row, "Bill #",
                        _bill.Id.ToString());
                    DetailCell(row, "Request #",
                        _bill.ServiceRequestId.ToString());
                    DetailCell(row, "Service",
                        _bill.ServiceRequest?.Category.ToString() ?? "—");
                    DetailCell(row, "Date issued",
                        _bill.CreatedAt.ToString("dd MMM yyyy"));
                    if (_bill.PaidAt.HasValue)
                        DetailCell(row, "Date paid",
                            _bill.PaidAt.Value.ToString("dd MMM yyyy"));
                });
        }

        void DetailCell(RowDescriptor row, string label, string value)
        {
            row.RelativeItem().Column(col =>
            {
                col.Item()
                    .Text(label)
                    .FontSize(9)
                    .FontColor(TextMuted)
                    .Bold();
                col.Item()
                    .Text(value)
                    .FontSize(12)
                    .Bold();
            });
        }

        // =============================================================
        //  ITEMS TABLE — line items with qty, price, amount
        // =============================================================
        void ComposeItemsTable(IContainer container)
        {
            container.Column(col =>
            {
                // Table header
                col.Item()
                    .Background(PrimaryColor)
                    .Padding(8)
                    .Row(row =>
                    {
                        row.RelativeItem()
                            .Text("Description")
                            .FontSize(11)
                            .FontColor(Colors.White)
                            .Bold();
                        row.ConstantItem(55)
                            .AlignCenter()
                            .Text("Qty")
                            .FontSize(11)
                            .FontColor(Colors.White)
                            .Bold();
                        row.ConstantItem(90)
                            .AlignRight()
                            .Text("Unit price")
                            .FontSize(11)
                            .FontColor(Colors.White)
                            .Bold();
                        row.ConstantItem(90)
                            .AlignRight()
                            .Text("Amount")
                            .FontSize(11)
                            .FontColor(Colors.White)
                            .Bold();
                    });

                // Table rows
                var items = _bill.BillItems.ToList();
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    Color rowBg = i % 2 == 0
                        ? Colors.White
                        : Color.FromHex("#F9FAFB");

                    col.Item()
                        .Background(rowBg)
                        .BorderBottom(0.5f)
                        .BorderColor(BorderColor)
                        .Padding(8)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Text(item.Description)
                                .FontSize(12);
                            row.ConstantItem(55)
                                .AlignCenter()
                                .Text(item.Quantity.ToString())
                                .FontSize(12)
                                .FontColor(TextMuted);
                            row.ConstantItem(90)
                                .AlignRight()
                                .Text($"₹{item.UnitPrice:N2}")
                                .FontSize(12)
                                .FontColor(TextMuted);
                            row.ConstantItem(90)
                                .AlignRight()
                                .Text($"₹{item.Amount:N2}")
                                .FontSize(12)
                                .Bold();
                        });
                }
            });
        }

        // =============================================================
        //  TOTAL ROW
        // =============================================================
        void ComposeTotal(IContainer container)
        {
            container
                .Background(LightColor)
                .Padding(12)
                .Row(row =>
                {
                    row.RelativeItem();  // spacer

                    row.ConstantItem(280).Column(col =>
                    {
                        col.Item().Row(r =>
                        {
                            r.RelativeItem()
                                .Text("Total amount")
                                .FontSize(14)
                                .Bold();
                            r.ConstantItem(120)
                                .AlignRight()
                                .Text($"₹{_bill.TotalAmount:N2}")
                                .FontSize(18)
                                .FontColor(PrimaryColor)
                                .Bold();
                        });
                    });
                });
        }

        // =============================================================
        //  PAYMENT STATUS BADGE
        // =============================================================
        void ComposePaymentStatus(IContainer container)
        {
            var isPaid = _bill.IsPaid;
            var bgColor = isPaid ? Color.FromHex("#D1FAE5") : Color.FromHex("#FEF3C7");
            var txColor = isPaid ? SuccessColor : Color.FromHex("#92400E");
            var label = isPaid ? "PAID" : "UNPAID";
            var icon = isPaid ? "Payment confirmed" : "Awaiting payment";

            container.Row(row =>
            {
                row.RelativeItem();
                row.AutoItem()
                    .Background(bgColor)
                    .Padding(10)
                    .Column(col =>
                    {
                        col.Item()
                            .Text(label)
                            .FontSize(13)
                            .FontColor(txColor)
                            .Bold();

                        col.Item()
                            .Text(icon)
                            .FontSize(10)
                            .FontColor(txColor);

                        // Payment method + transaction ID if paid
                        if (isPaid && _bill.Payment != null)
                        {
                            col.Item()
                                .PaddingTop(4)
                                .Text($"Method: {_bill.Payment.PaymentMethod}")
                                .FontSize(10)
                                .FontColor(txColor);

                            if (!string.IsNullOrEmpty(
                                _bill.Payment.GatewayTransactionId))
                            {
                                col.Item()
                                    .Text($"Ref: " +
                                          $"{_bill.Payment.GatewayTransactionId}")
                                    .FontSize(9)
                                    .FontColor(txColor);
                            }

                            col.Item()
                                .Text($"Paid on: " +
                                      $"{_bill.Payment.PaidAt:dd MMM yyyy}")
                                .FontSize(10)
                                .FontColor(txColor);
                        }
                    });
            });
        }

        // =============================================================
        //  NOTES — optional bill description
        // =============================================================
        void ComposeNotes(IContainer container)
        {
            container.Column(col =>
            {
                col.Item()
                    .Text("Notes")
                    .FontSize(11)
                    .FontColor(TextMuted)
                    .Bold();
                col.Item().PaddingTop(4)
                    .Text(_bill.Description)
                    .FontSize(11)
                    .FontColor(TextMuted);
            });
        }

        // =============================================================
        //  FOOTER — thank you note + page number
        // =============================================================
        void ComposeFooter(IContainer container)
        {
            container
                .PaddingTop(12)
                .BorderTop(0.5f)
                .BorderColor(BorderColor)
                .Row(row =>
                {
                    row.RelativeItem()
                        .Text("Thank you for using ServiceApp. " +
                              "For support: support@serviceapp.com")
                        .FontSize(9)
                        .FontColor(TextMuted);

                    row.AutoItem()
                        .Text(x =>
                        {
                            x.Span("Page ")
                                .FontSize(9)
                                .FontColor(TextMuted);
                            x.CurrentPageNumber()
                                .FontSize(9)
                                .FontColor(TextMuted);
                            x.Span(" of ")
                                .FontSize(9)
                                .FontColor(TextMuted);
                            x.TotalPages()
                                .FontSize(9)
                                .FontColor(TextMuted);
                        });
                });
        }
    }
}