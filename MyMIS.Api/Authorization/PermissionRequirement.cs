using Microsoft.AspNetCore.Authorization;

namespace MyMIS.Api.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}