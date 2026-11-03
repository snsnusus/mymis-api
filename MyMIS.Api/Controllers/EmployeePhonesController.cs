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
public class EmployeePhonesController(EmployeePhoneService service, EmployeeAccessGuard employeeAccessGuard) : ApiControllerBase
{
  private readonly EmployeePhoneService _service = service;
  private readonly EmployeeAccessGuard _access = employeeAccessGuard;

  [HttpGet("me/phones")]
  public async Task<ActionResult<List<EmployeePhoneResponseDto>>> GetMine()
  {
    if (User.GetEmployeeId() is not int employeeId)
    {
      return Unauthorized();
    }

    var phones = await _service.GetAllAsync(employeeId);
    return phones is null ? NotFound() : Ok(phones);
  }

  [HttpGet("me/phones/{phoneId:int}")]
  public async Task<ActionResult<EmployeePhoneResponseDto>> GetMineById(int phoneId)
  {
    if (User.GetEmployeeId() is not int employeeId)
    {
      return Unauthorized();
    }

    var phone = await _service.GetByIdAsync(employeeId, phoneId);

    return phone is null ? NotFound() : Ok(phone);
  }

  [HttpPost("me/phones")]
  public async Task<ActionResult<EmployeePhoneResponseDto>> CreateMine(EmployeePhoneCreateDto dto)
  {
    if (User.GetEmployeeId() is not int employeeId)
    {
      return Unauthorized();
    }

    var result = await _service.CreateAsync(employeeId, dto);
    return ToActionResult(result, value => StatusCode(StatusCodes.Status201Created, value));
  }

  [HttpPut("me/phones/{phoneId:int}")]
  public async Task<ActionResult<EmployeePhoneResponseDto>> UpdateMine(int phoneId, EmployeePhoneCreateDto dto)
  {
    if (User.GetEmployeeId() is not int employeeId) return Unauthorized();

    var result = await _service.UpdateAsync(employeeId, phoneId, dto);
    return ToActionResult(result, Ok);
  }

  [HttpDelete("me/phones/{phoneId:int}")]
  public async Task<IActionResult> DeleteMine(int phoneId)
  {
    if (User.GetEmployeeId() is not int employeeId) return Unauthorized();

    var result = await _service.DeleteAsync(employeeId, phoneId);
    return ToActionResult(result, _ => NoContent());
  }

  [Authorize(Policy = PermissionNames.Employees.Read)]
  [HttpGet("{id:int}/phones")]
  public async Task<ActionResult<List<EmployeePhoneResponseDto>>> GetForEmployee(int id)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Read) is { } denied) return denied;

    var phones = await _service.GetAllAsync(id);
    return phones is null ? NotFound() : Ok(phones);
  }

  [Authorize(Policy = PermissionNames.Employees.Read)]
  [HttpGet("{id:int}/phones/{phoneId:int}")]
  public async Task<ActionResult<EmployeePhoneResponseDto>> GetByIdForEmployee(int id, int phoneId)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Read) is { } denied) return denied;

    var phone = await _service.GetByIdAsync(id, phoneId);
    return phone is null ? NotFound() : Ok(phone);
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPost("{id:int}/phones")]
  public async Task<ActionResult<EmployeePhoneResponseDto>> CreateForEmployee(int id, EmployeePhoneCreateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.CreateAsync(id, dto);
    return ToActionResult(result, value => StatusCode(StatusCodes.Status201Created, value));
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPut("{id:int}/phones/{phoneId:int}")]
  public async Task<ActionResult<EmployeePhoneResponseDto>> UpdateForEmployee(int id, int phoneId, EmployeePhoneCreateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.UpdateAsync(id, phoneId, dto);
    return ToActionResult(result, Ok);
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpDelete("{id:int}/phones/{phoneId:int}")]
  public async Task<IActionResult> DeleteForEmployee(int id, int phoneId)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.DeleteAsync(id, phoneId);
    return ToActionResult(result, _ => NoContent());
  }
}