namespace ServiceApp.Core.Common;

/// <summary>Auto-assignment engine options ("AutoAssignment" section).</summary>
public class AutoAssignmentSettings
{
    public const string SectionName = "AutoAssignment";

    /// <summary>
    /// When no technician matches the request's zone, assign any available
    /// technician with the right skill. Off by default: the request stays Pending
    /// for an admin to assign manually.
    /// </summary>
    public bool AllowOutOfZoneFallback { get; set; }
}
