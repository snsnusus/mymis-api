using Microsoft.AspNetCore.Authorization;
using MyMIS.Api.Models;

namespace MyMIS.Api.Authorization;

public class SameDepartmentHandler : AuthorizationHandler<SameDepartmentRequirement, int>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SameDepartmentRequirement requirement,
        int targetDepartmentId)
    {
        var roleClaim = context.User.FindFirst("role")?.Value;

        if (roleClaim == Role.SuperAdmin.ToString())
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var departmentIdClaim = context.User.FindFirst("departmentId")?.Value;

        if (int.TryParse(departmentIdClaim, out var callerDepartmentId)
            && callerDepartmentId == targetDepartmentId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}