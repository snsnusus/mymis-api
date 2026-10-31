using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using MyMIS.Api.Authorization;
using MyMIS.Api.Models;
using Xunit;


namespace MyMIS.Api.Tests.Authorization;

public class PermissionHandlerTests
{
  // The role id every test's fake user carries. Any number works: the fake
  // cache is set up to answer for exactly this id.
  private const int RoleId = 3;
  private const int CallerDepartmentId = 9;

  // Builds a handler whose fake cache says "role 3 holds exactly these permissions".
  // `params` lets callers write HandlerWith(("employees.update", PermissionScope.Department))
  // with any number of (name, scope) pairs, or none at all.
  private static PermissionHandler HandlerWith(params (string Name, PermissionScope Scope)[] permissions)
  {
    // A tuple array → dictionary: name is the key, scope is the value.
    var dictionary = permissions.ToDictionary(p => p.Name, p => p.Scope);

    var cache = new Mock<IRolePermissionCache>();
    cache
      .Setup(c => c.GetAsync(RoleId))
      .ReturnsAsync(dictionary);

    return new PermissionHandler(cache.Object);
  }

  // Same idea as SameDepartmentHandlerTests.BuildUser, with the new claim names.
  private static ClaimsPrincipal BuildUser(string? roleId, string? departmentId = null)
  {
    var claims = new List<Claim>();

    if (roleId is not null)
    {
      claims.Add(new Claim("roleId", roleId));
    }

    if (departmentId is not null)
    {
      claims.Add(new Claim("departmentId", departmentId));
    }

    return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"));
  }

  [Fact]
  public async Task HandleAsync_NoRoleIdClaim_Fails()
  {
    // Arrange
    var handler = HandlerWith(("employees.update", PermissionScope.Company));
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("employees.update")],
      BuildUser(null),
      resource: null);

    // Act
    await handler.HandleAsync(context);

    // Assert
    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleAsync_LegacySuperAdminRoleClaimOnly_Fails()
  {
    // Arrange
    var handler = HandlerWith();
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("employees.create")],
      new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "SuperAdmin")], "TestAuthType")),
      resource: null);

    // Act
    await handler.HandleAsync(context);

    // Assert
    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleAsync_RoleLacksPermission_Fails()
  {
    // Arrange
    var handler = HandlerWith(("employees.update", PermissionScope.Company));
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("employees.create")],
      BuildUser(RoleId.ToString()),
      resource: null);

    // Act
    await handler.HandleAsync(context);

    // Assert
    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleAsync_RoleHasNoPermissions_Fails()
  {
    // Arrange
    var handler = HandlerWith();
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("employees.update")],
      BuildUser(RoleId.ToString()),
      resource: null
    );

    // Act
    await handler.HandleAsync(context);

    // Assert
    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleAsync_RoleHasPermission_NoResource_Succeeds()
  {
    // Arrange
    var handler = HandlerWith(("employees.update", PermissionScope.Company));
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("employees.update")],
      BuildUser(RoleId.ToString()),
      resource: null
    );

    // Act
    await handler.HandleAsync(context);

    // Assert
    Assert.True(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleAsync_DepartmentScopedPermission_AttributeGate_Succeeds()
  {
    // Arrange
    var handler = HandlerWith(("employees.update", PermissionScope.Department));
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("employees.update")],
      BuildUser(RoleId.ToString(), CallerDepartmentId.ToString()),
      resource: new DefaultHttpContext()
    );

    // Act
    await handler.HandleAsync(context);

    // Assert
    Assert.True(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleAsync_CompanyScope_AnyDepartment_Succeeds()
  {
    // Arrange
    var handler = HandlerWith(("employees.update", PermissionScope.Company));
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("employees.update")],
      BuildUser(RoleId.ToString(), CallerDepartmentId.ToString()),
      resource: 5
    );

    // Act
    await handler.HandleAsync(context);

    // Assert
    Assert.True(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleAsync_DepartmentScope_SameDepartment_Succeeds()
  {
    // Arrange
    var handler = HandlerWith(("employees.update", PermissionScope.Department));
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("employees.update")],
      BuildUser(RoleId.ToString(), CallerDepartmentId.ToString()),
      resource: CallerDepartmentId
    );

    // Act
    await handler.HandleAsync(context);

    // Assert
    Assert.True(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleAsync_DepartmentScope_OtherDepartment_Fails()
  {
    // Arrange
    var handler = HandlerWith(("employees.update", PermissionScope.Department));
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("employees.update")],
      BuildUser(RoleId.ToString(), CallerDepartmentId.ToString()),
      resource: 5
    );

    // Act
    await handler.HandleAsync(context);

    // Assert
    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleAsync_DepartmentScope_CallerHasNoDepartment_Fails()
  {
    // Arrange
    var handler = HandlerWith(("employees.update", PermissionScope.Department));
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("employees.update")],
      BuildUser(RoleId.ToString()),
      resource: CallerDepartmentId
    );

    // Act
    await handler.HandleAsync(context);

    // Assert
    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleAsync_UnexpectedResourceType_Fails()
  {
    // Arrange
    var handler = HandlerWith(("employees.update", PermissionScope.Company));
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("employees.update")],
      BuildUser(RoleId.ToString(), CallerDepartmentId.ToString()),
      resource: "oops"
    );

    // Act
    await handler.HandleAsync(context);

    // Assert
    Assert.False(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleAsync_DepartmentScope_UnassignedTarget_Fails()
  {
    // Arrange
    var handler = HandlerWith(("employees.update", PermissionScope.Department));
    var context = new AuthorizationHandlerContext(
      [new PermissionRequirement("employees.update")],
      BuildUser(RoleId.ToString(), CallerDepartmentId.ToString()),
      resource: -1
    );

    // Act
    await handler.HandleAsync(context);

    // Assert
    Assert.False(context.HasSucceeded);
  }
}