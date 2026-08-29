// =================================================================
//  IAutoAssignmentService.cs — ServiceApp.Core/Interfaces
//
//  Contract for the background auto-assignment engine.
//
//  HOW IT WORKS (like Rapido's dispatch system):
//  1. Hangfire triggers RunAsync() every 2 minutes
//  2. Fetch all Pending requests ordered by CreatedAt (oldest first)
//  3. For each request → find best Available technician:
//       - Skill must match request Category
//       - Status must be Available
//       - Sort by Rating descending (best rated first)
//       - Tiebreaker: least recently assigned (fairness)
//  4. If match found → assign atomically + notify via SignalR
//  5. If no match → skip, try again next cycle
//
//  FAIRNESS RULES:
//  - Oldest request gets assigned first (FIFO queue)
//  - Best rated technician gets priority
//  - If ratings equal → least recently assigned tech wins
//    (prevents same tech from getting all jobs)
// =================================================================

namespace ServiceApp.Core.Interfaces
{
    public interface IAutoAssignmentService
    {
        // Main engine method — called by Hangfire every 2 minutes.
        // Processes ALL pending requests in one cycle.
        Task RunAsync();

        // Assign one specific request — called manually if needed.
        // Returns true if assignment succeeded, false if no tech available.
        Task<bool> TryAssignRequestAsync(int requestId);
    }
}