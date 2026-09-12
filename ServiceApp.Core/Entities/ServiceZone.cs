// =================================================================
//  ServiceZone.cs — ServiceApp.Core/Entities
//
//  A geographic zone (city or area) that groups pin codes.
//  Technicians belong to a zone. Requests are matched to zones.
//  Auto-assignment only assigns technicians whose zone covers
//  the request's pin code.
//
//  EXAMPLE:
//  Zone: "Thrissur"
//  Pin codes: ["680001","680002","680003","680303","680301"]
//
//  A Thrissur technician only gets jobs from those pin codes.
//  This prevents a Kochi technician being assigned to a Thrissur job.
// =================================================================

namespace ServiceApp.Core.Entities
{
    public class ServiceZone
    {
        public int ZoneId { get; set; }
        public string ZoneName { get; set; } = string.Empty; // "Thrissur"
        public string City { get; set; } = string.Empty; // "Thrissur"
        public string State { get; set; } = string.Empty; // "Kerala"
        public bool IsActive { get; set; } = true;

        // Comma-separated pin codes this zone covers
        // e.g. "680001,680002,680003,680303"
        // Stored as a string — simple, no extra join table needed
        public string PinCodes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Helper — parse pin codes into a list
        public List<string> GetPinCodeList() =>
            PinCodes
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToList();

        // Check if a pin code belongs to this zone
        public bool ContainsPinCode(string pinCode) =>
            GetPinCodeList().Contains(pinCode.Trim());
    }
}