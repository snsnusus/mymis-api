
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
using MyMIS.Api.Options;

namespace MyMIS.Api.Services;

public class AuthService(AppDbContext context, TokenService tokenService, IOptions<JwtOptions> jwtOptions)
{
    private readonly AppDbContext _context = context;
    private readonly TokenService _tokenService = tokenService;
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Username == dto.Username);

        if (employee is null || !BCrypt.Net.BCrypt.Verify(dto.Password, employee.PasswordHash))
        {
            return null;
        }

        var accessToken = _tokenService.GenerateAccessToken(employee);
        var rawRefreshToken = _tokenService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            TokenHash = _tokenService.HashToken(rawRefreshToken),
            EmployeeId = employee.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays)
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken
        };
    }

    public async Task<AuthResponseDto?> RefreshAsync(RefreshRequestDto dto)
    {
        var incomingHash = _tokenService.HashToken(dto.RefreshToken);

        var existingToken = await _context.RefreshTokens
            .Include(rt => rt.Employee)
            .FirstOrDefaultAsync(rt => rt.TokenHash == incomingHash);

        if (existingToken is null)
        {
            return null;
        }

        if (existingToken.RevokedAt is not null)
        {
            var activeTokensForEmployee = await _context.RefreshTokens
                .Where(rt => rt.EmployeeId == existingToken.EmployeeId && rt.RevokedAt == null)
                .ToListAsync();

            foreach (var token in activeTokensForEmployee)
            {
                token.RevokedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return null;
        }

        if (existingToken.ExpiresAt < DateTime.UtcNow)
        {
            return null;
        }

        existingToken.RevokedAt = DateTime.UtcNow;

        var newAccessToken = _tokenService.GenerateAccessToken(existingToken.Employee);
        var newRawRefreshToken = _tokenService.GenerateRefreshToken();

        var newRefreshToken = new RefreshToken
        {
            TokenHash = _tokenService.HashToken(newRawRefreshToken),
            EmployeeId = existingToken.EmployeeId,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpiryDays)
        };

        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken = newAccessToken,
            RefreshToken = newRawRefreshToken
        };
    }

    public async Task<bool> LogoutAsync(LogoutRequestDto dto)
    {
        var incomingHash = _tokenService.HashToken(dto.RefreshToken);

        var existingToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == incomingHash && rt.RevokedAt == null);

        if (existingToken is null)
        {
            return false;
        }

        existingToken.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePasswordAsync(int employeeId, ChangePasswordDto dto)
    {
        var employee = await _context.Employees.FindAsync(employeeId);

        if (employee is null || !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, employee.PasswordHash))
        {
            return false;
        }

        employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.EmployeeId == employeeId && rt.RevokedAt == null)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }
}