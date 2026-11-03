using MyMIS.Api.Models;

namespace MyMIS.Api.Authorization;

public static class PermissionScopeExtensions
{
  // Does a HELD scope cover a REQUESTED one?
  // Company covers both; Department covers only Department.
  //
  // `this PermissionScope held` makes it an extension method, so callers write
  // held.Covers(requested), as if Covers were built into the enum.
  // That's the same trick as User.GetRoleId() on ClaimsPrincipal.
  public static bool Covers(this PermissionScope held, PermissionScope requested) =>
    held == PermissionScope.Company || requested == PermissionScope.Department;
}