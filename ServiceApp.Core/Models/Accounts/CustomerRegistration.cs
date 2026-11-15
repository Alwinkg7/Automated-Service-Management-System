namespace ServiceApp.Core.Models.Accounts;

/// <summary>Input for registering a new customer account.</summary>
public record CustomerRegistration(
    string FullName,
    string Email,
    string Phone,
    string Password,
    string? ReferralCode);
