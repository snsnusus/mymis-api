using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/Employees")]
[Authorize]
public class EmployeeGovernmentIdsController(EmployeeGovernmentIdService service) : ApiControllerBase
{
  private readonly EmployeeGovernmentIdService _service = service;

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

  [HttpGet("{id:int}/government-ids")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<EmployeeGovernmentIdResponseDto>> GetForEmployee(int id)
  {
    var governmentIds = await _service.GetAsync(id);
    return governmentIds is null ? NotFound() : Ok(governmentIds);
  }

  [HttpPut("{id:int}/government-ids")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<EmployeeGovernmentIdResponseDto>> UpdateForEmployee(int id, EmployeeGovernmentIdUpdateDto dto)
  {
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