using CogSlop.Api.Models.Dtos;
using CogSlop.Api.Models.Requests;
using CogSlop.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CogSlop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    ICurrentUserService currentUserService,
    IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = "/api/auth/post-login",
        };

        return Challenge(properties);
    }

    [Authorize]
    [HttpGet("post-login")]
    public async Task<IActionResult> PostLogin(CancellationToken cancellationToken)
    {
        await currentUserService.EnsureUserAsync(User, cancellationToken);

        var frontendBaseUrl = ResolveFrontendBaseUrl(configuration);
        return Redirect($"{frontendBaseUrl}/");
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> Me(CancellationToken cancellationToken)
    {
        var profile = await currentUserService.GetProfileAsync(User, cancellationToken);
        return Ok(profile);
    }

    [Authorize]
    [HttpPut("display-name")]
    public async Task<ActionResult<UserProfileDto>> UpdateDisplayName(
        [FromBody] UpdateDisplayNameRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = await currentUserService.UpdateDisplayNameAsync(
                User,
                request.DisplayName,
                cancellationToken);

            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Cog clutch released. You are logged out." });
    }

    private static string ResolveFrontendBaseUrl(IConfiguration config)
    {
        var configured = config["Frontend:BaseUrl"] ?? "http://localhost:4200";

        var firstOrigin = configured
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.TrimEnd('/'))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        return string.IsNullOrWhiteSpace(firstOrigin)
            ? "http://localhost:4200"
            : firstOrigin;
    }
}
