// ServiceApp.Services/Implementations/TechnicianService.cs
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Services.Implementations;

public class TechnicianService : ITechnicianService
{
    private readonly IUnitOfWork _uow;

    public TechnicianService(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<TechnicianProfile>> GetByUserIdAsync(string userId)
    {
        var tech = await _uow.TechnicianProfiles.GetByUserIdAsync(userId);
        return tech == null
            ? Result<TechnicianProfile>.Failure("Technician profile not found.")
            : Result<TechnicianProfile>.Success(tech);
    }

    public async Task<Result<TechnicianStatus>> ToggleAvailabilityAsync(string userId)
    {
        var tech = await _uow.TechnicianProfiles.GetByUserIdAsync(userId);
        if (tech == null)
            return Result<TechnicianStatus>.Failure(
                "Technician profile not found.");

        if (tech.Status == TechnicianStatus.Busy)
            return Result<TechnicianStatus>.Failure(
                "Cannot toggle status while you have an active job.");

        tech.Status = tech.Status == TechnicianStatus.Available
            ? TechnicianStatus.Offline
            : TechnicianStatus.Available;

        _uow.TechnicianProfiles.Update(tech);
        await _uow.SaveChangesAsync();

        return Result<TechnicianStatus>.Success(tech.Status);
    }
}