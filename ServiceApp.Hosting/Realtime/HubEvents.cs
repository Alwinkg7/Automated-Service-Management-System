namespace ServiceApp.Hosting.Realtime;

/// <summary>Client-side event names pushed by the server. Documented in docs/API.md.</summary>
public static class HubEvents
{
    public const string JobAssigned = "JobAssigned";
    public const string NewJobAvailable = "NewJobAvailable";
    public const string JobAccepted = "JobAccepted";
    public const string BillCreated = "BillCreated";
    public const string JobCompleted = "JobCompleted";
    public const string NewRequest = "NewRequest";
    public const string StatusChanged = "StatusChanged";
    public const string LocationUpdate = "LocationUpdate";
    public const string LocationStopped = "LocationStopped";
}
