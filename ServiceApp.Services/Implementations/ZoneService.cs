using Microsoft.Extensions.Logging;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Services.Implementations;

/// <summary>Service-zone administration with input normalisation and validation.</summary>
public class ZoneService : IZoneService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ZoneService> _logger;

    public ZoneService(IUnitOfWork uow, ILogger<ZoneService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ServiceZone>> GetAllAsync(bool includeInactive)
    {
        var zones = includeInactive
            ? await _uow.ServiceZones.GetAllAsync()
            : await _uow.ServiceZones.GetActiveZonesAsync();
        return zones.OrderBy(z => z.ZoneName).ToList();
    }

    public async Task<Result<ServiceZone>> GetAsync(int zoneId) =>
        await _uow.ServiceZones.GetByIdAsync(zoneId) is { } zone
            ? Result<ServiceZone>.Success(zone)
            : Result<ServiceZone>.Failure("Zone not found.", ErrorType.NotFound);

    public async Task<Result<ServiceZone>> CreateAsync(ZoneInput input)
    {
        var error = Validate(input, out var pinCodes);
        if (error != null) return Result<ServiceZone>.Failure(error);

        if (await _uow.ServiceZones.AnyAsync(z => z.ZoneName == input.ZoneName.Trim()))
            return Result<ServiceZone>.Failure("A zone with this name already exists.", ErrorType.Conflict);

        if (await FindPinConflictAsync(pinCodes, exceptZoneId: null) is { } clashCreate)
            return Result<ServiceZone>.Failure(clashCreate, ErrorType.Conflict);

        var zone = new ServiceZone
        {
            ZoneName = input.ZoneName.Trim(),
            City = input.City.Trim(),
            State = input.State.Trim(),
            PinCodes = pinCodes,
            IsActive = input.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        await _uow.ServiceZones.AddAsync(zone);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Zone {ZoneId} created", zone.ZoneId);
        return Result<ServiceZone>.Success(zone);
    }

    public async Task<Result<ServiceZone>> UpdateAsync(int zoneId, ZoneInput input)
    {
        var zone = await _uow.ServiceZones.GetByIdAsync(zoneId);
        if (zone == null) return Result<ServiceZone>.Failure("Zone not found.", ErrorType.NotFound);

        var error = Validate(input, out var pinCodes);
        if (error != null) return Result<ServiceZone>.Failure(error);

        var name = input.ZoneName.Trim();
        if (await _uow.ServiceZones.AnyAsync(z => z.ZoneName == name && z.ZoneId != zoneId))
            return Result<ServiceZone>.Failure("A zone with this name already exists.", ErrorType.Conflict);

        if (await FindPinConflictAsync(pinCodes, zoneId) is { } clashUpdate)
            return Result<ServiceZone>.Failure(clashUpdate, ErrorType.Conflict);

        zone.ZoneName = name;
        zone.City = input.City.Trim();
        zone.State = input.State.Trim();
        zone.PinCodes = pinCodes;
        zone.IsActive = input.IsActive;
        _uow.ServiceZones.Update(zone);
        await _uow.SaveChangesAsync();
        return Result<ServiceZone>.Success(zone);
    }

    public async Task<Result<ServiceZone>> ToggleAsync(int zoneId)
    {
        var zone = await _uow.ServiceZones.GetByIdAsync(zoneId);
        if (zone == null) return Result<ServiceZone>.Failure("Zone not found.", ErrorType.NotFound);

        zone.IsActive = !zone.IsActive;
        _uow.ServiceZones.Update(zone);
        await _uow.SaveChangesAsync();
        return Result<ServiceZone>.Success(zone);
    }

    /// <summary>Normalises pin codes (comma/space/newline separated, at least 4 chars, distinct).</summary>
    public static string NormalizePinCodes(string? raw) =>
        string.Join(",",
            (raw ?? string.Empty)
                .Split([',', '\n', '\r', ' ', ';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Distinct());

    /// <summary>Returns an error when any pin code already belongs to another active zone.</summary>
    private async Task<string?> FindPinConflictAsync(string pinCodes, int? exceptZoneId)
    {
        var wanted = pinCodes.Split(',').ToHashSet();
        foreach (var other in await _uow.ServiceZones.GetActiveZonesAsync())
        {
            if (other.ZoneId == exceptZoneId) continue;
            var clash = other.GetPinCodeList().FirstOrDefault(wanted.Contains);
            if (clash != null)
                return $"Pin code {clash} already belongs to zone {other.ZoneName}.";
        }
        return null;
    }

    private static string? Validate(ZoneInput input, out string pinCodes)
    {
        pinCodes = NormalizePinCodes(input.PinCodes);
        if (string.IsNullOrWhiteSpace(input.ZoneName) || input.ZoneName.Trim().Length > 100)
            return "Zone name is required (max 100 characters).";
        if (string.IsNullOrWhiteSpace(input.City) || string.IsNullOrWhiteSpace(input.State))
            return "City and state are required.";
        if (pinCodes.Length == 0)
            return "Enter at least one pin code.";
        if (pinCodes.Split(',').Any(p => p.Length != 6 || !p.All(char.IsDigit)))
            return "Pin codes must be 6 digits.";
        return null;
    }
}
