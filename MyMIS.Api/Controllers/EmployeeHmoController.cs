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
public class EmployeeHmoController(EmployeeHmoService service, EmployeeAccessGuard employeeAccessGuard) : ApiControllerBase
{
  private readonly EmployeeHmoService _service = service;
  private readonly EmployeeAccessGuard _access = employeeAccessGuard;

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

  [Authorize(Policy = PermissionNames.Employees.Read)]
  [HttpGet("{id:int}/hmo")]
  public async Task<ActionResult<EmployeeHmoResponseDto>> GetForEmployee(int id)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Read) is { } denied) return denied;

    var hmo = await _service.GetActiveAsync(id);
    return hmo is null ? NotFound() : Ok(hmo);
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPost("{id:int}/hmo")]
  public async Task<ActionResult<EmployeeHmoResponseDto>> CreateForEmployee(int id, EmployeeHmoCreateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.CreateAsync(id, dto);
    return ToActionResult(result, value => StatusCode(StatusCodes.Status201Created, value));
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPut("{id:int}/hmo")]
  public async Task<ActionResult<EmployeeHmoResponseDto>> UpdateForEmployee(int id, EmployeeHmoUpdateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.UpdateAsync(id, dto);
    return ToActionResult(result, Ok);
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpDelete("{id:int}/hmo")]
  public async Task<IActionResult> DeleteForEmployee(int id)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.DeleteAsync(id);
    return ToActionResult(result, _ => NoContent());
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPost("{id:int}/hmo/dependents")]
  public async Task<ActionResult<EmployeeHmoResponseDto>> AddDependent(int id, EmployeeHmoDependentCreateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.AddDependentAsync(id, dto);
    return ToActionResult(result, value => StatusCode(StatusCodes.Status201Created, value));
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPut("{id:int}/hmo/dependents/{dependentId:int}")]
  public async Task<ActionResult<EmployeeHmoResponseDto>> UpdateDependent(
      int id,
      int dependentId,
      EmployeeHmoDependentCreateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.UpdateDependentAsync(id, dependentId, dto);
    return ToActionResult(result, Ok);
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpDelete("{id:int}/hmo/dependents/{dependentId:int}")]
  public async Task<IActionResult> DeleteDependent(int id, int dependentId)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.DeleteDependentAsync(id, dependentId);
    return ToActionResult(result, _ => NoContent());
  }
}