using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using MyMIS.Api.Models;

namespace MyMIS.Api.Authorization;

// One handler for every permission policy. It answers:
//   gate 1 (attribute):     does the caller's role hold this permission at all?
//   gate 2 (AuthorizeAsync with a department id): does it cover THAT department?
//
// The primary constructor receives the cache from DI. Both are singletons,
// so holding it in a field for the app's lifetime is safe.
public class PermissionHandler(IRolePermissionCache rolePermissions)
  : AuthorizationHandler<PermissionRequirement>
{
  protected override async Task HandleRequirementAsync(
      AuthorizationHandlerContext context,
      PermissionRequirement requirement)
  {
    // No (or unreadable) roleId claim → do nothing → the requirement stays
    // unsatisfied → 403. A token issued before Step 4 has no roleId; the
    // portal's refresh replaces it within one access-token lifetime.
    if (!int.TryParse(context.User.FindFirst("roleId")?.Value, out var roleId))
    {
      return;
    }

    var permissions = await rolePermissions.GetAsync(roleId);

    // Dictionary.TryGetValue: true/false + the value via `out`, like Map.has + Map.get in one call.
    if (!permissions.TryGetValue(requirement.Permission, out var scope))
    {
      return;
    }

    // `is int targetDepartmentId` checks the type AND names the value in one step,
    // roughly `typeof x === 'number'` plus `const targetDepartmentId = x` in JS.
    if (context.Resource is int targetDepartmentId)
    {
      // Gate 2: a specific department.
      if (scope == PermissionScope.Company)
      {
        context.Succeed(requirement);
        return;
      }

      // Department scope: only the caller's own department. A caller with no
      // departmentId claim fails TryParse → fails closed. A target of -1 (the
      // existing "unassigned" sentinel) never equals a real department id.
      if (int.TryParse(context.User.FindFirst("departmentId")?.Value, out var callerDepartmentId)
          && callerDepartmentId == targetDepartmentId)
      {
        context.Succeed(requirement);
      }

      return;
    }

    // Gate 1: the attribute check. Endpoint routing passes the HttpContext;
    // null covers direct AuthorizeAsync(User, policy) calls with no resource.
    // `null or HttpContext` is a pattern: "is null, OR is an HttpContext".
    if (context.Resource is null or HttpContext)
    {
      context.Succeed(requirement);
    }

    // Any other resource type: unexpected → fail closed (do nothing).
  }
}