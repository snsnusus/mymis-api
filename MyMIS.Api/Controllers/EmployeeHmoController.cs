using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/Employees")]
[Authorize]
public class EmployeeHmoController(EmployeeHmoService service) : ApiControllerBase
{
  private readonly EmployeeHmoService _service = service;

  // ---------------- self-service (read-only) ----------------

  [HttpGet("me/hmo")]
  public async Task<ActionResult<EmployeeHmoResponseDto>> GetMine()
  {
    if (User.GetEmployeeId() is not int employeeId)
    {
      return Unauthorized();
    }

    var hmo = await _service.GetActiveAsync(employeeId);
    return hmo is null ? NotFound() : Ok(hmo);
  }

  // ---------------- HR: enrollment ----------------

  [HttpGet("{id:int}/hmo")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<EmployeeHmoResponseDto>> GetForEmployee(int id)
  {
    var hmo = await _service.GetActiveAsync(id);
    return hmo is null ? NotFound() : Ok(hmo);
  }

  [HttpPost("{id:int}/hmo")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<EmployeeHmoResponseDto>> CreateForEmployee(int id, EmployeeHmoCreateDto dto)
  {
    var result = await _service.CreateAsync(id, dto);
    return ToActionResult(result, value => StatusCode(StatusCodes.Status201Created, value));
  }

  [HttpPut("{id:int}/hmo")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<EmployeeHmoResponseDto>> UpdateForEmployee(int id, EmployeeHmoUpdateDto dto)
  {
    var result = await _service.UpdateAsync(id, dto);
    return ToActionResult(result, Ok);
  }

  [HttpDelete("{id:int}/hmo")]
  [Authorize(Policy = "employees.update")]
  public async Task<IActionResult> DeleteForEmployee(int id)
  {
    var result = await _service.DeleteAsync(id);
    return ToActionResult(result, _ => NoContent());
  }

  // ---------------- HR: dependents ----------------

  [HttpPost("{id:int}/hmo/dependents")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<EmployeeHmoResponseDto>> AddDependent(int id, EmployeeHmoDependentCreateDto dto)
  {
    var result = await _service.AddDependentAsync(id, dto);
    return ToActionResult(result, value => StatusCode(StatusCodes.Status201Created, value));
  }

  [HttpPut("{id:int}/hmo/dependents/{dependentId:int}")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<EmployeeHmoResponseDto>> UpdateDependent(
      int id,
      int dependentId,
      EmployeeHmoDependentCreateDto dto)
  {
    var result = await _service.UpdateDependentAsync(id, dependentId, dto);
    return ToActionResult(result, Ok);
  }

  [HttpDelete("{id:int}/hmo/dependents/{dependentId:int}")]
  [Authorize(Policy = "employees.update")]
  public async Task<IActionResult> DeleteDependent(int id, int dependentId)
  {
    var result = await _service.DeleteDependentAsync(id, dependentId);
    return ToActionResult(result, _ => NoContent());
  }
}