using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MyMIS.Api.Authorization;
using MyMIS.Api.Models;

namespace MyMIS.Api.Tests.Authorization;

public class DepartmentScopeHandlerTests
{
    private readonly DepartmentScopeHandler _handler = new();

    private static ClaimsPrincipal BuildUser(string? role, string? departmentId)
    {
        var claims = new List<Claim>();

        if (role is not null)
        {
            claims.Add(new Claim("role", role));
        }

        if (departmentId is not null)
        {
            claims.Add(new Claim("departmentId", departmentId));
        }

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task HandleAsync_SuperAdminRole_SucceedsRegardlessOfDepartment()
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new DepartmentScopeRequirement()],
            BuildUser("SuperAdmin", "1"),
            resource: 5);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_AdminRole_SucceedsWithMatchingDepartment()
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new DepartmentScopeRequirement()],
            BuildUser("Admin", "1"),
            resource: 1);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_AdminRole_FailsWithNonMatchingDepartment()
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new DepartmentScopeRequirement()],
            BuildUser("Admin", "1"),
            resource: 2);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_AdminRole_FailsAgainstUnassignedDepartmentSentinel()
    {
        // Arrange
        var context = new AuthorizationHandlerContext(
            [new DepartmentScopeRequirement()],
            BuildUser("Admin", "1"),
            resource: -1);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }
}