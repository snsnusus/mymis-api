using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

// Who holds which role (design doc §6c). Not part of the public profile:
// reading needs roles.read, changing needs roles.assign. Both are company-only,
// so there's no gate 2 / EmployeeAccessGuard here.
[ApiController]
[Route("api/Employees")]
[Authorize]
public class EmployeeRolesController(RoleAssignmentService service) : ApiControllerBase
{
  private readonly RoleAssignmentService _service = service;

  [HttpGet("{id:int}/role")]
  [Authorize(Policy = PermissionNames.Roles.Read)]
  public async Task<ActionResult<EmployeeRoleResponseDto>> Get(int id)
  {
    var role = await _service.GetAsync(id);
    return role is null ? NotFound() : Ok(role);
  }

  [HttpPut("{id:int}/role")]
  [Authorize(Policy = PermissionNames.Roles.Assign)]
  public async Task<ActionResult<EmployeeRoleResponseDto>> Assign(int id, EmployeeRoleUpdateDto dto)
  {
    // Both come from the caller's token: who they are (for "not your own role")
    // and what role they hold (for the escalation guard). Unreachable in practice,
    // since the policy above already needed a valid roleId.
    if (User.GetEmployeeId() is not int callerEmployeeId || User.GetRoleId() is not int callerRoleId)
    {
      return Forbid();
    }

    // dto.RoleId!.Value: [Required] already rejected a missing roleId.
    var result = await _service.AssignAsync(id, dto.RoleId!.Value, callerEmployeeId, callerRoleId);
    return ToActionResult(result, Ok);
  }
}