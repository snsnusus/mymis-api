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
public class EmployeeEmailsController(EmployeeEmailService service, EmployeeAccessGuard employeeAccessGuard) : ApiControllerBase
{
  private readonly EmployeeEmailService _service = service;
  private readonly EmployeeAccessGuard _access = employeeAccessGuard;

  [HttpGet("me/emails")]
  public async Task<ActionResult<List<EmployeeEmailResponseDto>>> GetMine()
  {
    if (User.GetEmployeeId() is not int employeeId)
    {
      return Unauthorized();
    }

    var emails = await _service.GetAllAsync(employeeId);
    return emails is null ? NotFound() : Ok(emails);
  }

  [HttpGet("me/emails/{emailId:int}")]
  public async Task<ActionResult<EmployeeEmailResponseDto>> GetMineById(int emailId)
  {
    if (User.GetEmployeeId() is not int employeeId)
    {
      return Unauthorized();
    }

    var email = await _service.GetByIdAsync(employeeId, emailId);

    return email is null ? NotFound() : Ok(email);
  }

  [HttpPost("me/emails")]
  public async Task<ActionResult<EmployeeEmailResponseDto>> CreateMine(EmployeeEmailCreateDto dto)
  {
    if (User.GetEmployeeId() is not int employeeId)
    {
      return Unauthorized();
    }

    var result = await _service.CreateAsync(employeeId, dto);
    return ToActionResult(result, value => StatusCode(StatusCodes.Status201Created, value));
  }

  [HttpPut("me/emails/{emailId:int}")]
  public async Task<ActionResult<EmployeeEmailResponseDto>> UpdateMine(int emailId, EmployeeEmailCreateDto dto)
  {
    if (User.GetEmployeeId() is not int employeeId) return Unauthorized();

    var result = await _service.UpdateAsync(employeeId, emailId, dto);
    return ToActionResult(result, Ok);
  }

  [HttpDelete("me/emails/{emailId:int}")]
  public async Task<IActionResult> DeleteMine(int emailId)
  {
    if (User.GetEmployeeId() is not int employeeId) return Unauthorized();

    var result = await _service.DeleteAsync(employeeId, emailId);
    return ToActionResult(result, _ => NoContent());
  }

  [Authorize(Policy = PermissionNames.Employees.Read)]
  [HttpGet("{id:int}/emails")]
  public async Task<ActionResult<List<EmployeeEmailResponseDto>>> GetForEmployee(int id)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Read) is { } denied) return denied;

    var emails = await _service.GetAllAsync(id);
    return emails is null ? NotFound() : Ok(emails);
  }

  [Authorize(Policy = PermissionNames.Employees.Read)]
  [HttpGet("{id:int}/emails/{emailId:int}")]
  public async Task<ActionResult<EmployeeEmailResponseDto>> GetByIdForEmployee(int id, int emailId)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Read) is { } denied) return denied;

    var email = await _service.GetByIdAsync(id, emailId);
    return email is null ? NotFound() : Ok(email);
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPost("{id:int}/emails")]
  public async Task<ActionResult<EmployeeEmailResponseDto>> CreateForEmployee(int id, EmployeeEmailCreateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.CreateAsync(id, dto);
    return ToActionResult(result, value => StatusCode(StatusCodes.Status201Created, value));
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPut("{id:int}/emails/{emailId:int}")]
  public async Task<ActionResult<EmployeeEmailResponseDto>> UpdateForEmployee(int id, int emailId, EmployeeEmailCreateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.UpdateAsync(id, emailId, dto);
    return ToActionResult(result, Ok);
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpDelete("{id:int}/emails/{emailId:int}")]
  public async Task<IActionResult> DeleteForEmployee(int id, int emailId)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.DeleteAsync(id, emailId);
    return ToActionResult(result, _ => NoContent());
  }
}