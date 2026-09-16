using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MyMIS.Api.Authorization;
using Xunit;

namespace MyMIS.Api.Tests.Authorization;

public class PermissionHandlerTests
{
    private readonly PermissionHandler _handler = new();

    private static ClaimsPrincipal BuildUser(string? role, params string[] permissions)
    {
        var claims = new List<Claim>();

        if (role is not null)
            claims.Add(new Claim("role", role));

        foreach (var permission in permissions)
            claims.Add(new Claim("permission", permission));

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task HandleAsync_SuperAdminRole_SucceedsRegardlessOfPermission()
    {
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement("employees.create")],
            BuildUser("SuperAdmin"),
            resource: null
        );

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_UserRole_WithMatchingPermission_Succeeds()
    {
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement("employees.create")],
            BuildUser("User", "employees.update", "employees.create"),
            resource: null
        );

        await _handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_UserRole_WithoutMatchingPermission_Fails()
    {
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement("employees.create")],
            BuildUser("User", "employees.update"),   // holds a permission, just not this one
            resource: null
        );

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_UserRole_NoPermissionsAtAll_Fails()
    {
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement("employees.create")],
            BuildUser("User"),
            resource: null
        );

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_AdminRole_WithoutMatchingPermission_Fails()
    {
        // This is the intentional breaking change flagged earlier: Admin gets
        // no automatic bypass the way SuperAdmin does. Admin-without-the-
        // permission must fail, exactly like a plain User would.
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement("employees.create")],
            BuildUser("Admin"),
            resource: null
        );

        await _handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}


