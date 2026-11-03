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
public class EmployeeAddressesController(EmployeeAddressService service, EmployeeAccessGuard employeeAccessGuard) : ControllerBase
{
  private readonly EmployeeAddressService _service = service;
  private readonly EmployeeAccessGuard _access = employeeAccessGuard;

  [HttpGet("me/addresses")]
  public async Task<ActionResult<List<EmployeeAddressResponseDto>>> GetMine()
  {
    if (User.GetEmployeeId() is not int employeeId) return Unauthorized();

    var addresses = await _service.GetAllAsync(employeeId);
    return addresses is null ? NotFound() : Ok(addresses);
  }

  [HttpPost("me/addresses")]
  public async Task<ActionResult<EmployeeAddressResponseDto>> CreateMine(EmployeeAddressCreateDto dto)
  {
    if (User.GetEmployeeId() is not int employeeId) return Unauthorized();

    var result = await _service.CreateAsync(employeeId, dto);
    return ToActionResult(result, value => StatusCode(StatusCodes.Status201Created, value));
  }

  [HttpPut("me/addresses/{addressId:int}")]
  public async Task<ActionResult<EmployeeAddressResponseDto>> UpdateMine(int addressId, EmployeeAddressCreateDto dto)
  {
    if (User.GetEmployeeId() is not int employeeId) return Unauthorized();

    var result = await _service.UpdateAsync(employeeId, addressId, dto);
    return ToActionResult(result, value => Ok(value));
  }

  [HttpDelete("me/addresses/{addressId:int}")]
  public async Task<IActionResult> DeleteMine(int addressId)
  {
    if (User.GetEmployeeId() is not int employeeId) return Unauthorized();

    var result = await _service.DeleteAsync(employeeId, addressId);
    return ToActionResult(result, _ => NoContent());
  }

  [Authorize(Policy = PermissionNames.Employees.Read)]
  [HttpGet("{id:int}/addresses")]
  public async Task<ActionResult<List<EmployeeAddressResponseDto>>> GetForEmployee(int id)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Read) is { } denied) return denied;

    var addresses = await _service.GetAllAsync(id);
    return addresses is null ? NotFound() : Ok(addresses);
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPost("{id:int}/addresses")]
  public async Task<ActionResult<EmployeeAddressResponseDto>> CreateForEmployee(int id, EmployeeAddressCreateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.CreateAsync(id, dto);
    return ToActionResult(result, value => StatusCode(StatusCodes.Status201Created, value));
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPut("{id:int}/addresses/{addressId:int}")]
  public async Task<ActionResult<EmployeeAddressResponseDto>> UpdateForEmployee(int id, int addressId, EmployeeAddressCreateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.UpdateAsync(id, addressId, dto);
    return ToActionResult(result, value => Ok(value));
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpDelete("{id:int}/addresses/{addressId:int}")]
  public async Task<IActionResult> DeleteForEmployee(int id, int addressId)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.DeleteAsync(id, addressId);
    return ToActionResult(result, _ => NoContent());
  }

  // Translates a service outcome into an HTTP response. The caller decides
  // what success looks like (201, 200 or 204); errors use { message }.
  private ActionResult ToActionResult<T>(ServiceResult<T> result, Func<T, ActionResult> onSuccess) =>
      result.ErrorType switch
      {
        null => onSuccess(result.Value!),
        ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
        ServiceErrorType.Validation => BadRequest(new { message = result.ErrorMessage }),
        _ => throw new InvalidOperationException($"Unhandled service error type: {result.ErrorType}"),
      };
}