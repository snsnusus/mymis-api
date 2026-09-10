using Microsoft.AspNetCore.Authorization;
using MyMIS.Api.Models;

namespace MyMIS.Api.Authorization;

public class DepartmentScopeHandler : AuthorizationHandler<DepartmentScopeRequirement, int>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DepartmentScopeRequirement requirement,
        int targetDepartmentId)
    {
        var roleClaim = context.User.FindFirst("role")?.Value;

        if (roleClaim == Role.SuperAdmin.ToString())
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (roleClaim == Role.Admin.ToString())
        {
            var departmentIdClaim = context.User.FindFirst("departmentId")?.Value;

            if (int.TryParse(departmentIdClaim, out var adminDepartmentId)
                && adminDepartmentId == targetDepartmentId)
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}