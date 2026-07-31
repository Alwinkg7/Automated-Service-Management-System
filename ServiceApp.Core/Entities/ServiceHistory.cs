// =================================================================
//  ServiceHistory.cs — ServiceApp.Core/Entities
//
//  Append-only audit log. Every time a ServiceRequest.Status
//  changes, we INSERT one row here. We never UPDATE this table.
//
//  EXAMPLE ROWS for RequestId = 5:
//  | Status     | Note                        | ChangedBy | Time  |
//  |------------|-----------------------------|-----------|-------|
//  | Pending    | Request created             | customer  | 10:00 |
//  | Assigned   | Technician John assigned    | admin     | 10:15 |
//  | InProgress | Technician accepted the job | tech      | 10:30 |
//  | Billed     | Bill of ₹850 created        | tech      | 12:00 |
//  | Completed  | Payment confirmed           | system    | 12:05 |
//
//  Displayed as a timeline on the Request Details page.
// =================================================================

using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Entities
{
    public class ServiceHistory
    {
        public int HistoryId { get; set; }
        public int RequestId { get; set; }
        public RequestStatus Status { get; set; }
        public string? Note { get; set; } = string.Empty;
        public string ChangedByUserId { get; set; } = string.Empty;
        public string? ChangedById { get; set; }  // ← ADD — exists in DB
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        // Navigation — must be named 'Request' NOT 'ServiceRequest'
        public ServiceRequest Request { get; set; } = null!;
    }
}