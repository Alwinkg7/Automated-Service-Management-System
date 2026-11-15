// ServiceApp.Core/Interfaces/ITechnicianService.cs
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Interfaces;

public interface ITechnicianService
{
    Task<Result<TechnicianProfile>> GetByUserIdAsync(string userId);
    Task<Result<TechnicianStatus>> ToggleAvailabilityAsync(string userId);

    /// <summary>Admin override of availability status.</summary>
    Task<Result<TechnicianProfile>> OverrideStatusAsync(int technicianProfileId, TechnicianStatus status);

    /// <summary>Admin change of a technician skill. Refused (409) while the technician has an active job.</summary>
    Task<Result<TechnicianProfile>> UpdateSkillAsync(int technicianProfileId, ServiceCategory skill);
}