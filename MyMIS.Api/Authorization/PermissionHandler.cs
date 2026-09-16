using Microsoft.AspNetCore.Authorization;
using MyMIS.Api.Models;

namespace MyMIS.Api.Authorization;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
    AuthorizationHandlerContext context,
    PermissionRequirement requirement)
    {
        var roleClaim = context.User.FindFirst("role")?.Value;

        if (roleClaim == Role.SuperAdmin.ToString())
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var hasPermission = context.User.Claims
            .Any(c => c.Type == "permission" && c.Value == requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}