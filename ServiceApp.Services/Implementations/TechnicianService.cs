
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
            ? Result<TechnicianProfile>.Failure("Technician profile not found.", ErrorType.NotFound)
            : Result<TechnicianProfile>.Success(tech);
    }

    public async Task<Result<TechnicianStatus>> ToggleAvailabilityAsync(string userId)
    {
        var tech = await _uow.TechnicianProfiles.GetByUserIdAsync(userId);
        if (tech == null)
            return Result<TechnicianStatus>.Failure("Technician profile not found.", ErrorType.NotFound);

        if (tech.Status == TechnicianStatus.Busy)
            return Result<TechnicianStatus>.Failure("Cannot toggle status while you have an active job.", ErrorType.Conflict);

        tech.Status = tech.Status == TechnicianStatus.Available
            ? TechnicianStatus.Offline
            : TechnicianStatus.Available;

        _uow.TechnicianProfiles.Update(tech);
        await _uow.SaveChangesAsync();

        return Result<TechnicianStatus>.Success(tech.Status);
    }

    public async Task<Result<TechnicianProfile>> OverrideStatusAsync(int technicianProfileId, TechnicianStatus status)
    {
        if (!Enum.IsDefined(status))
            return Result<TechnicianProfile>.Failure("Invalid status.");

        var tech = await _uow.TechnicianProfiles.GetByIdAsync(technicianProfileId);
        if (tech == null)
            return Result<TechnicianProfile>.Failure("Technician not found.", ErrorType.NotFound);

        tech.Status = status;
        _uow.TechnicianProfiles.Update(tech);
        await _uow.SaveChangesAsync();
        return Result<TechnicianProfile>.Success(tech);
    }

    public async Task<Result<TechnicianProfile>> UpdateSkillAsync(int technicianProfileId, ServiceCategory skill)
    {
        if (!Enum.IsDefined(skill))
            return Result<TechnicianProfile>.Failure("Invalid skill.");

        var tech = await _uow.TechnicianProfiles.GetByIdAsync(technicianProfileId);
        if (tech == null)
            return Result<TechnicianProfile>.Failure("Technician not found.", ErrorType.NotFound);

        var hasActiveJob = await _uow.ServiceRequests.AnyAsync(r =>
            r.AssignedTechnicianProfileId == technicianProfileId
            && (r.Status == RequestStatus.Assigned || r.Status == RequestStatus.InProgress || r.Status == RequestStatus.Billed));
        if (hasActiveJob)
            return Result<TechnicianProfile>.Failure("Cannot change the skill while the technician has an active job.", ErrorType.Conflict);

        tech.Skill = skill;
        _uow.TechnicianProfiles.Update(tech);
        await _uow.SaveChangesAsync();
        return Result<TechnicianProfile>.Success(tech);
    }
}
