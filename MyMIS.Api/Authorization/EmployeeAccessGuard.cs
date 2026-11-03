using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.Services;

namespace MyMIS.Api.Authorization;

// Gate 2 for endpoints that act on ONE employee (/api/Employees/{id}/...).
// Gate 1 (the [Authorize(Policy = ...)] attribute) has already run by the time
// this is called, so the caller holds the permission at SOME scope; this
// decides whether it covers THIS employee's department.
//
// Scoped, like EmployeeService, which it depends on (one per HTTP request).
public class EmployeeAccessGuard(EmployeeService employees, IAuthorizationService authorization)
{
  // Returns null when access is allowed (the action continues),
  // or the response to send instead: 404 if the employee doesn't exist,
  // 403 if the permission doesn't cover their department.
  //
  // Like Express middleware that either calls next() or sends a response:
  // `null` = next(), a result = res.status(...).end().
  public async Task<ActionResult?> CheckAsync(ClaimsPrincipal user, int employeeId, string permission)
  {
    // Existing helper: one query that answers "does it exist, and where is it?"
    // Soft-deleted employees count as missing (EF's query filter hides them).
    var (exists, departmentId) = await employees.GetExistenceAndDepartmentAsync(employeeId);

    if (!exists)
    {
      return new NotFoundResult();
    }

    // Gate 2: the int resource tells PermissionHandler which department to check.
    // An employee with no department becomes -1, which a department-scoped
    // permission can never match (company-scoped ones still pass).
    var result = await authorization.AuthorizeAsync(user, departmentId ?? -1, permission);

    // ForbidResult → 403 through the normal auth pipeline, the same as when an
    // [Authorize] attribute rejects a request.
    return result.Succeeded ? null : new ForbidResult();
  }
}