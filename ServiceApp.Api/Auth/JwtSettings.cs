using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ServiceApp.Api.Auth;

/// <summary>JWT configuration ("Jwt" section). Validated at startup.</summary>
public class JwtSettings : IValidatableObject
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 signing key; must be at least 256 bits (32 bytes). Keep in user-secrets.</summary>
    [Required]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Access-token lifetime in minutes.</summary>
    [Range(1, 60 * 24 * 30)]
    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>Refresh-token lifetime in days.</summary>
    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Encoding.UTF8.GetByteCount(Key) < 32)
            yield return new ValidationResult(
                "Jwt:Key must be at least 32 bytes (256 bits). Set it with dotnet user-secrets.",
                [nameof(Key)]);
    }
}
