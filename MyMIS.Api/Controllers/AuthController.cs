using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Services;
using System.IdentityModel.Tokens.Jwt;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService) : ControllerBase
{
    private readonly AuthService _authService = authService;

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);

        if (result is null)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> Refresh(RefreshRequestDto dto)
    {
        var result = await _authService.RefreshAsync(dto);

        if (result is null)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequestDto dto)
    {
        var success = await _authService.LogoutAsync(dto);

        if (!success)
        {
            return Unauthorized(new { message = "Invalid or already-expired refresh token." });
        }

        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var employeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (employeeIdClaim is null || !int.TryParse(employeeIdClaim, out var employeeId))
        {
            return Unauthorized();
        }

        var success = await _authService.ChangePasswordAsync(employeeId, dto);

        if (!success)
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        return NoContent();
    }
}