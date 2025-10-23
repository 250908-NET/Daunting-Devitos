using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Project.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    // GET /auth/login
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        Console.WriteLine($"[AUTH] Login called with returnUrl: {returnUrl}");

        // Allow frontend URLs (localhost:3000 or your production frontend)
        var allowedOrigins = new[] { "http://localhost:3000", "https://localhost:3000" };
        var safe = "/swagger"; // default fallback

        if (!string.IsNullOrEmpty(returnUrl))
        {
            // Check if returnUrl is a local path or starts with an allowed origin
            if (
                Url.IsLocalUrl(returnUrl)
                || allowedOrigins.Any(origin => returnUrl.StartsWith(origin))
            )
            {
                safe = returnUrl;
                Console.WriteLine($"[AUTH] returnUrl accepted: {safe}");
            }
            else
            {
                Console.WriteLine($"[AUTH] returnUrl rejected, using fallback: {safe}");
            }
        }
        else
        {
            Console.WriteLine($"[AUTH] No returnUrl provided, using fallback: {safe}");
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            Console.WriteLine($"[AUTH] User already authenticated, redirecting to: {safe}");
            return Redirect(safe);
        }

        var props = new AuthenticationProperties { RedirectUri = safe };
        props.SetParameter("prompt", "select_account");

        Console.WriteLine($"[AUTH] Challenging with RedirectUri: {safe}");
        return Challenge(props, GoogleDefaults.AuthenticationScheme);
    }

    // POST /auth/logout
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logged out" });
    }

    // GET /auth/me
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var user = HttpContext.User;
        return Ok(
            new
            {
                name = user.Identity?.Name,
                claims = user.Claims.Select(c => new { c.Type, c.Value }),
            }
        );
    }
}
