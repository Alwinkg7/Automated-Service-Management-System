using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;

namespace ServiceApp.Core.Interfaces;

/// <summary>Admin management of service zones (geo matching for auto-assignment).</summary>
public interface IZoneService
{
    Task<IReadOnlyList<ServiceZone>> GetAllAsync(bool includeInactive);
    Task<Result<ServiceZone>> GetAsync(int zoneId);
    Task<Result<ServiceZone>> CreateAsync(ZoneInput input);
    Task<Result<ServiceZone>> UpdateAsync(int zoneId, ZoneInput input);
    Task<Result<ServiceZone>> ToggleAsync(int zoneId);
}

/// <summary>Zone fields. <see cref="PinCodes"/> may be separated by commas, spaces or new lines.</summary>
public record ZoneInput(string ZoneName, string City, string State, string PinCodes, bool IsActive = true);
