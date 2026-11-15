using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceApp.Api.Auth;
using ServiceApp.Api.Contracts;
using ServiceApp.Api.Extensions;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;
using ServiceApp.Core.Models.Accounts;

namespace ServiceApp.Api.Controllers;

/// <summary>Login, registration, token refresh and logout for API clients (Next.js, Flutter).</summary>
[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public class AuthApiController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAccountService _accounts;
    private readonly ITokenService _tokens;

    public AuthApiController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IAccountService accounts,
        ITokenService tokens)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _accounts = accounts;
        _tokens = tokens;
    }

    /// <summary>Exchanges email + password for an access token and a refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
            return Fail(StatusCodes.Status401Unauthorized, "Invalid credentials.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return Fail(StatusCodes.Status401Unauthorized, "Too many failed attempts. Account locked for 5 minutes.");
        if (!result.Succeeded)
            return Fail(StatusCodes.Status401Unauthorized, "Invalid credentials.");

        return Ok(AuthBody(user, await _tokens.IssueAsync(user)));
    }

    /// <summary>Rotates a refresh token: the old one is consumed and a new pair is returned.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var refreshed = await _tokens.RefreshAsync(request.RefreshToken);
        if (refreshed is not { } r)
            return Fail(StatusCodes.Status401Unauthorized, "Invalid or expired refresh token.");

        return Ok(AuthBody(r.User, r.Tokens));
    }

    /// <summary>Revokes the given refresh token. Always succeeds.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await _tokens.RevokeAsync(request.RefreshToken);
        return NoContent();
    }

    /// <summary>Registers a customer and signs them in.</summary>
    [HttpPost("register/customer")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterCustomer([FromBody] RegisterCustomerRequest request)
    {
        var result = await _accounts.RegisterCustomerAsync(new CustomerRegistration(
            request.FullName, request.Email, request.Phone, request.Password, request.ReferralCode));
        if (!result.IsSuccess)
            return Fail(result);

        return CreatedAtAction(nameof(Me), null, AuthBody(result.Data!, await _tokens.IssueAsync(result.Data!)));
    }

    /// <summary>Registers a technician and signs them in.</summary>
    [HttpPost("register/technician")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterTechnician([FromBody] RegisterTechnicianRequest request)
    {
        var result = await _accounts.RegisterTechnicianAsync(new TechnicianRegistration(
            request.FullName, request.Email, request.Phone, request.Password, request.Skill));
        if (!result.IsSuccess)
            return Fail(result);

        return CreatedAtAction(nameof(Me), null, AuthBody(result.Data!, await _tokens.IssueAsync(result.Data!)));
    }

    /// <summary>Current user's profile from the access token.</summary>
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [DisableRateLimiting]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user == null || !user.IsActive)
            return Fail(StatusCodes.Status401Unauthorized, "User no longer exists or is inactive.");

        return Ok(new
        {
            success = true,
            data = new
            {
                userId = user.Id,
                name = user.FullName,
                email = user.Email,
                phone = user.Phone,
                role = user.Role.ToString()
            }
        });
    }

    // Field names token/role/userId/name/email are the original Flutter contract.
    private static object AuthBody(ApplicationUser user, AuthTokens tokens) => new
    {
        success = true,
        token = tokens.AccessToken,
        expiresAt = tokens.AccessTokenExpiresAt,
        refreshToken = tokens.RefreshToken,
        refreshTokenExpiresAt = tokens.RefreshTokenExpiresAt,
        role = user.Role.ToString(),
        userId = user.Id,
        name = user.FullName,
        email = user.Email
    };
}
