using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HrETracker.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, IHostEnvironment environment) : ControllerBase
{
    private const string RefreshCookie = "hr_et_refresh";

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("authentication")]
    public async Task<ActionResult<BrowserSessionResponse>> Login(LoginRequest request,
        [FromHeader(Name = "X-HR-Auth")] string? sessionHeader, CancellationToken cancellationToken)
    {
        if (sessionHeader != "1") return StatusCode(403, new ProblemDetails { Title = "The X-HR-Auth header is required." });
        var result = await authService.LoginAsync(request, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return result is null ? Unauthorized(new ProblemDetails { Title = "Invalid email or password." }) : SessionResponse(result);
    }

    [AllowAnonymous, HttpPost("refresh"), EnableRateLimiting("authentication")]
    public async Task<ActionResult<BrowserSessionResponse>> Refresh(
        [FromHeader(Name = "X-HR-Auth")] string? sessionHeader, CancellationToken cancellationToken)
    {
        if (sessionHeader != "1") return StatusCode(403, new ProblemDetails { Title = "The X-HR-Auth header is required." });
        Response.Headers.CacheControl = "no-store";
        var token = Request.Cookies[RefreshCookie];
        var result = string.IsNullOrEmpty(token) ? null : await authService.RefreshAsync(token, cancellationToken);
        if (result is not null) return SessionResponse(result);
        Response.Cookies.Delete(RefreshCookie, CookieOptions());
        return Unauthorized(new ProblemDetails { Title = "Invalid or expired session." });
    }

    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromHeader(Name = "X-HR-Auth")] string? sessionHeader, CancellationToken cancellationToken)
    {
        if (sessionHeader != "1") return StatusCode(403, new ProblemDetails { Title = "The X-HR-Auth header is required." });
        Response.Headers.CacheControl = "no-store";
        Response.Cookies.Delete(RefreshCookie, CookieOptions());
        if (User.Identity?.IsAuthenticated == true)
            await authService.LogoutAsync(Guid.Parse(User.FindFirstValue("sid")!), UserId, cancellationToken);
        if (Request.Cookies[RefreshCookie] is { Length: > 0 } token)
            await authService.LogoutByRefreshTokenAsync(token, cancellationToken);
        return NoContent();
    }

    [Authorize, HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var user = await authService.GetUserAsync(UserId, cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    [Authorize, HttpPost("change-password"), EnableRateLimiting("authentication")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.ChangePasswordAsync(UserId, request, cancellationToken);
        if (result.Succeeded)
        {
            Response.Cookies.Delete(RefreshCookie, CookieOptions());
            return NoContent();
        }
        foreach (var error in result.Errors) ModelState.AddModelError("password", error.Description);
        return ValidationProblem(ModelState);
    }

    private string UserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    private CookieOptions CookieOptions() => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment() || Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        IsEssential = true
    };

    private OkObjectResult SessionResponse(AuthenticatedUserResponse result)
    {
        var options = CookieOptions();
        options.Expires = new DateTimeOffset(DateTime.SpecifyKind(result.RefreshTokenExpiresAtUtc, DateTimeKind.Utc));
        Response.Cookies.Append(RefreshCookie, result.RefreshToken, options);
        return Ok(new BrowserSessionResponse(result.AccessToken, result.ExpiresAtUtc, result.Email,
            result.FullName, result.Roles, result.RefreshTokenExpiresAtUtc));
    }
}
