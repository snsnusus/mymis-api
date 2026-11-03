using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/Employees")]
[Authorize]
public class EmployeeGovernmentIdsController(EmployeeGovernmentIdService service, EmployeeAccessGuard employeeAccessGuard) : ApiControllerBase
{
  private readonly EmployeeGovernmentIdService _service = service;
  private readonly EmployeeAccessGuard _access = employeeAccessGuard;

  [HttpGet("me/government-ids")]
  public async Task<ActionResult<EmployeeGovernmentIdResponseDto>> GetMine()
  {
    if (User.GetEmployeeId() is not int employeeId)
    {
      return Unauthorized();
    }

    var governmentIds = await _service.GetAsync(employeeId);
    return governmentIds is null ? NotFound() : Ok(governmentIds);
  }

  [Authorize(Policy = PermissionNames.Employees.Read)]
  [HttpGet("{id:int}/government-ids")]
  public async Task<ActionResult<EmployeeGovernmentIdResponseDto>> GetForEmployee(int id)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Read) is { } denied) return denied;

    var governmentIds = await _service.GetAsync(id);
    return governmentIds is null ? NotFound() : Ok(governmentIds);
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPut("{id:int}/government-ids")]
  public async Task<ActionResult<EmployeeGovernmentIdResponseDto>> UpdateForEmployee(int id, EmployeeGovernmentIdUpdateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    if (User.GetEmployeeId() is not int callerId)
    {
      return Unauthorized();
    }

    if (id == callerId)
    {
      return Forbid();
    }

    var result = await _service.UpdateAsync(id, callerId, dto);
    return ToActionResult(result, Ok);
  }
}