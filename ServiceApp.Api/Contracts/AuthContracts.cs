using System.ComponentModel.DataAnnotations;
using ServiceApp.Core.Enums;

namespace ServiceApp.Api.Contracts;

/// <summary>POST /api/auth/login</summary>
public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

/// <summary>POST /api/auth/refresh and /api/auth/logout</summary>
public record RefreshRequest([Required] string RefreshToken);

/// <summary>POST /api/auth/register/customer</summary>
public record RegisterCustomerRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [Required, EmailAddress] string Email,
    [Required, StringLength(15, MinimumLength = 10)] string Phone,
    [Required, StringLength(100, MinimumLength = 8)] string Password,
    string? ReferralCode);

/// <summary>POST /api/auth/register/technician</summary>
public record RegisterTechnicianRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [Required, EmailAddress] string Email,
    [Required, StringLength(15, MinimumLength = 10)] string Phone,
    [Required, StringLength(100, MinimumLength = 8)] string Password,
    [Required, EnumDataType(typeof(ServiceCategory))] ServiceCategory Skill);
