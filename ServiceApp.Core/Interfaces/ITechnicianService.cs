// ServiceApp.Core/Interfaces/ITechnicianService.cs
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Interfaces;

public interface ITechnicianService
{
    Task<Result<TechnicianProfile>> GetByUserIdAsync(string userId);
    Task<Result<TechnicianStatus>> ToggleAvailabilityAsync(string userId);
}