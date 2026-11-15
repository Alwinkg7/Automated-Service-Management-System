namespace ServiceApp.Hosting.Realtime;

/// <summary>SignalR group names shared by <see cref="ServiceHub"/> and the notification service.</summary>
public static class HubGroups
{
    public const string Admin = "admin";
    public const string TechniciansAvailable = "technicians-available";

    public static string Customer(string userId) => $"customer-{userId}";
    public static string Technician(string userId) => $"technician-{userId}";
    public static string Personal(string role, string userId) => $"{role.ToLowerInvariant()}-{userId}";
}
