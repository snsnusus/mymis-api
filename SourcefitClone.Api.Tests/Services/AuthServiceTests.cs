using Microsoft.EntityFrameworkCore;
using MSOPtions = Microsoft.Extensions.Options.Options;
using SourcefitClone.Api.Data;
using SourcefitClone.Api.DTOs;
using SourcefitClone.Api.Options;
using SourcefitClone.Api.Services;
using SourcefitClone.Api.Models;

namespace SourcefitClone.Api.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly AuthService _service;
    private readonly TokenService _tokenService;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        var jwtOptions = MSOPtions.Create(new JwtOptions
        {
            Key = "this-is-a-test-signing-key-at-least-32-bytes-long",
            Issuer = "test-issuer",
            Audience = "test-audience",
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7
        });

        _tokenService = new TokenService(jwtOptions);
        _service = new AuthService(_context, _tokenService, jwtOptions);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokens()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            Username = "johndoe",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "E-2026-050",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("johndoe"),
            Role = Role.User,
        };
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        // Act
        var tokens = await _service.LoginAsync(new LoginDto { Username = "johndoe", Password = "johndoe" });

        // Assert
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));

        var savedToken = await _context.RefreshTokens
    .FirstOrDefaultAsync(rt => rt.EmployeeId == employee.Id);

        Assert.NotNull(savedToken);
        Assert.Null(savedToken.RevokedAt);
    }

    [Fact]
    public async Task LoginAsync_ExistingUser_InvalidPassword_ReturnsNull()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "Jane",
            LastName = "Doe",
            Username = "janedoe",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP001",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("this-is-the-correct-password"),
            Role = Role.User,
        };
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.LoginAsync(new LoginDto { Username = "janedoe", Password = "wrong-password" });

        // Assert
        Assert.Null(result);

        var tokenCount = await _context.RefreshTokens.CountAsync(rt => rt.EmployeeId == employee.Id);
        Assert.Equal(0, tokenCount);
    }

    [Fact]
    public async Task LoginAsync_NonExistingUser_ReturnsNull()
    {
        // Act
        var result = await _service.LoginAsync(new LoginDto { Username = "nonexistentuser", Password = "any-password" });

        // Assert
        Assert.Null(result);

        var tokenCount = await _context.RefreshTokens.CountAsync();
        Assert.Equal(0, tokenCount);
    }

    [Fact]
    public async Task RefreshAsync_NoRefreshTokenExists_ReturnsNull()
    {
        // Act
        var result = await _service.RefreshAsync(new RefreshRequestDto { RefreshToken = "nonexistent-refresh-token" });

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshAsync_RevokedToken_ReturnsNull()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "Alice",
            LastName = "Smith",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP002",
            Username = "alicesmith",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("alicesmith"),
            Role = Role.User,
        };
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        var revokedToken = new RefreshToken
        {
            TokenHash = _tokenService.HashToken("revoked-refresh-token"),
            EmployeeId = employee.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.RefreshTokens.Add(revokedToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RefreshAsync(new RefreshRequestDto { RefreshToken = "revoked-refresh-token" });

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshAsync_RevokedTokenReused_RevokesAllActiveTokensForThatEmployeeOnly()
    {
        // Arrange
        var employeeOne = new Employee
        {
            FirstName = "Alice",
            LastName = "Smith",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP002",
            Username = "alicesmith",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("alicesmith"),
            Role = Role.User,
        };
        var employeeTwo = new Employee
        {
            FirstName = "Bob",
            LastName = "Johnson",
            Gender = "MALE",
            MaritalStatus = "MARRIED",
            EmployeeCode = "EMP003",
            Username = "bobjohnson",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("bobjohnson"),
            Role = Role.User,
        };
        _context.Employees.AddRange(employeeOne, employeeTwo);
        await _context.SaveChangesAsync();

        var employeeOneRefreshTokenOne = new RefreshToken
        {
            TokenHash = _tokenService.HashToken("revoked-refresh-token-emp1"),
            EmployeeId = employeeOne.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow.AddDays(-1)
        };
        var employeeOneRefreshTokenTwo = new RefreshToken
        {
            TokenHash = _tokenService.HashToken("active-refresh-token-emp1-2"),
            EmployeeId = employeeOne.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null
        };
        var employeeOneRefreshTokenThree = new RefreshToken
        {
            TokenHash = _tokenService.HashToken("active-refresh-token-emp1-3"),
            EmployeeId = employeeOne.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null
        };
        var employeeTwoRefreshTokenOne = new RefreshToken
        {
            TokenHash = _tokenService.HashToken("active-refresh-token-emp2-1"),
            EmployeeId = employeeTwo.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null
        };

        _context.RefreshTokens.AddRange(
            employeeOneRefreshTokenOne, employeeOneRefreshTokenTwo,
            employeeOneRefreshTokenThree, employeeTwoRefreshTokenOne);
        await _context.SaveChangesAsync();

        // Act
        await _service.RefreshAsync(new RefreshRequestDto { RefreshToken = "revoked-refresh-token-emp1" });

        // Assert
        var tokenCountEmployeeOne = await _context.RefreshTokens
            .CountAsync(rt => rt.EmployeeId == employeeOne.Id && rt.RevokedAt == null);
        Assert.Equal(0, tokenCountEmployeeOne);

        var tokenCountEmployeeTwo = await _context.RefreshTokens
            .CountAsync(rt => rt.EmployeeId == employeeTwo.Id && rt.RevokedAt == null);
        Assert.Equal(1, tokenCountEmployeeTwo);
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_ReturnsNull()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "Charlie",
            LastName = "Brown",
            Gender = "MALE",
            EmployeeCode = "EMP004",
            MaritalStatus = "SINGLE",
            Username = "charliebrown",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("charliebrown"),
            Role = Role.User,
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        var token = new RefreshToken
        {
            TokenHash = _tokenService.HashToken("refresh-token"),
            EmployeeId = employee.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired token
            RevokedAt = null,
        };

        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RefreshAsync(new RefreshRequestDto { RefreshToken = "refresh-token" });

        // Assert
        Assert.Null(result);

        var existingToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.EmployeeId == employee.Id);
        Assert.Null(existingToken?.RevokedAt);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsNewTokens()
    {

        const string rawToken = "original-refresh-token";
        // Arrange
        var employee = new Employee
        {
            FirstName = "David",
            LastName = "Green",
            Gender = "MALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP005",
            Username = "davidgreen",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("davidgreen"),
            Role = Role.User,
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        var originalRefreshToken = new RefreshToken
        {
            TokenHash = _tokenService.HashToken(rawToken),
            EmployeeId = employee.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null,
        };

        _context.RefreshTokens.Add(originalRefreshToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RefreshAsync(new RefreshRequestDto { RefreshToken = rawToken });

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));

        var oldToken = await _context.RefreshTokens.FirstAsync(rt => rt.TokenHash == _tokenService.HashToken(rawToken));
        Assert.NotNull(oldToken.RevokedAt);

        var totalTokenCount = await _context.RefreshTokens.CountAsync(rt => rt.EmployeeId == employee.Id);
        Assert.Equal(2, totalTokenCount); // old (now revoked) + new
    }

    [Fact]
    public async Task LogoutAsync_ValidToken_ReturnsTrueAndRevokesToken()
    {
        // Arrange
        const string rawToken = "logout-refresh-token";

        var employee = new Employee
        {
            FirstName = "Eve",
            LastName = "White",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP006",
            Username = "evewhite",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("evewhite"),
            Role = Role.User,
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        var refreshToken = new RefreshToken
        {
            TokenHash = _tokenService.HashToken(rawToken),
            EmployeeId = employee.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null,
        };
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.LogoutAsync(new LogoutRequestDto { RefreshToken = rawToken });

        // Assert
        Assert.True(result);

        var token = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.EmployeeId == employee.Id);
        Assert.NotNull(token?.RevokedAt);
    }

    [Fact]
    public async Task LogoutAsync_RevokedToken_ReturnsFalse()
    {
        // Arrange
        const string rawToken = "revoked-refresh-token";

        var employee = new Employee
        {
            FirstName = "Frank",
            LastName = "Black",
            Gender = "MALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP007",
            Username = "frankblack",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("frankblack"),
            Role = Role.User,
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        var revokedToken = new RefreshToken
        {
            TokenHash = _tokenService.HashToken(rawToken),
            EmployeeId = employee.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow.AddDays(-1), // Already revoked
        };
        _context.RefreshTokens.Add(revokedToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.LogoutAsync(new LogoutRequestDto { RefreshToken = rawToken });

        // Assert
        Assert.False(result);

        var token = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.EmployeeId == employee.Id);
        Assert.NotNull(token?.RevokedAt); // Should still be revoked
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsFalseAndDoesNotRevokedToken()
    {
        // Arrange
        const string rawToken = "grace-refresh-token";

        var employee = new Employee
        {
            FirstName = "Grace",
            LastName = "Hopper",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP008",
            Username = "gracehopper",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("gracehopper"),
            Role = Role.User,
        };
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        var refreshToken = new RefreshToken
        {
            TokenHash = _tokenService.HashToken(rawToken),
            EmployeeId = employee.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null,
        };
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ChangePasswordAsync(employee.Id, new ChangePasswordDto
        {
            CurrentPassword = "wrongpassword",
            NewPassword = "newsecurepassword",
        });


        // Assert
        Assert.False(result);

        var updatedEmployee = await _context.Employees.FindAsync(employee.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify("gracehopper", updatedEmployee?.PasswordHash)); // Password should not be changed

        var token = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.EmployeeId == employee.Id);
        Assert.Null(token?.RevokedAt); // Token should not be revoked
    }

    [Fact]
    public async Task ChangePasswordAsync_CorrectCurrentPassword_ReturnsTrue()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "Henry",
            LastName = "Ford",
            Gender = "MALE",
            MaritalStatus = "MARRIED",
            EmployeeCode = "EMP009",
            Username = "henryford",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("henryford"),
            Role = Role.User,
        };
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ChangePasswordAsync(employee.Id, new ChangePasswordDto
        {
            CurrentPassword = "henryford",
            NewPassword = "newsecurepassword",
        });

        // Assert
        Assert.True(result);

        var updatedEmployee = await _context.Employees.FindAsync(employee.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify("newsecurepassword", updatedEmployee?.PasswordHash)); // Password should be changed
        Assert.False(BCrypt.Net.BCrypt.Verify("henryford", updatedEmployee?.PasswordHash)); // Old password should not match
    }

    [Fact]
    public async Task ChangePasswordAsync_CorrectCurrentPassword_RevokesAllActiveTokens()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "Ivy",
            LastName = "Lee",
            Gender = "FEMALE",
            MaritalStatus = "SINGLE",
            EmployeeCode = "EMP010",
            Username = "ivylee",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ivylee"),
            Role = Role.User,
        };
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        var refreshToken1 = new RefreshToken
        {
            TokenHash = _tokenService.HashToken("refresh-token-1"),
            EmployeeId = employee.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null,
        };
        var refreshToken2 = new RefreshToken
        {
            TokenHash = _tokenService.HashToken("refresh-token-2"),
            EmployeeId = employee.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null,
        };
        var refreshToken3 = new RefreshToken
        {
            TokenHash = _tokenService.HashToken("refresh-token-3"),
            EmployeeId = employee.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null,
        };
        _context.RefreshTokens.AddRange(refreshToken1, refreshToken2, refreshToken3);
        await _context.SaveChangesAsync();

        // Act
        await _service.ChangePasswordAsync(employee.Id, new ChangePasswordDto
        {
            CurrentPassword = "ivylee",
            NewPassword = "newsecurepassword",
        });

        // Assert
        var updatedRefreshTokens = await _context.RefreshTokens.Where(rt => rt.EmployeeId == employee.Id).ToListAsync();
        Assert.All(updatedRefreshTokens, rt => Assert.NotNull(rt.RevokedAt));
    }
}