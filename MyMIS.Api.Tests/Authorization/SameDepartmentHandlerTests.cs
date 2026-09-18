using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MyMIS.Api.Authorization;

namespace MyMIS.Api.Tests.Authorization;

public class SameDepartmentHandlerTests
{
    private readonly SameDepartmentHandler _handler = new();

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
        var context = new AuthorizationHandlerContext(
            [new SameDepartmentRequirement()],
            BuildUser("SuperAdmin", "1"),
            resource: 5);

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_UserRoleWithMatchingDepartment_Succeeds()
    {
        var context = new AuthorizationHandlerContext(
            [new SameDepartmentRequirement()],
            BuildUser("User", "3"),
            resource: 3);

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_AdminRoleWithMatchingDepartment_Succeeds()
    {
        var context = new AuthorizationHandlerContext(
            [new SameDepartmentRequirement()],
            BuildUser("Admin", "3"),
            resource: 3);

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_NonMatchingDepartment_Fails()
    {
        var context = new AuthorizationHandlerContext(
            [new SameDepartmentRequirement()],
            BuildUser("User", "3"),
            resource: 5);

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_MissingDepartmentClaim_Fails()
    {
        var context = new AuthorizationHandlerContext(
            [new SameDepartmentRequirement()],
            BuildUser("User", null),
            resource: 3);

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}