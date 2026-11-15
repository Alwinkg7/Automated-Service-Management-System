using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Models.Accounts;

/// <summary>Input for registering a new technician account.</summary>
public record TechnicianRegistration(
    string FullName,
    string Email,
    string Phone,
    string Password,
    ServiceCategory Skill);
