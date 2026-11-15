namespace ServiceApp.Hosting.Options;

/// <summary>
/// SignalR scale-out. When <see cref="RedisConnectionString"/> is set, both hosts
/// share a Redis backplane so a notification raised in one host reaches clients
/// connected to the other.
/// </summary>
public class RealtimeSettings
{
    public const string SectionName = "SignalR";

    public string? RedisConnectionString { get; set; }
    public string ChannelPrefix { get; set; } = "asms";
}
