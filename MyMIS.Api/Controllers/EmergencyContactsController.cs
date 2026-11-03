using System.IdentityModel.Tokens.Jwt;
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
public class EmergencyContactsController(EmergencyContactService service, EmployeeAccessGuard employeeAccessGuard) : ControllerBase
{
  private readonly EmergencyContactService _service = service;
  private readonly EmployeeAccessGuard _access = employeeAccessGuard;

  [HttpGet("me/emergency-contacts")]
  public async Task<ActionResult<List<EmergencyContactResponseDto>>> GetMine()
  {
    if (GetCurrentEmployeeId() is not int employeeId)
    {
      return Unauthorized();
    }

    var contacts = await _service.GetAllAsync(employeeId);
    return contacts is null ? NotFound() : Ok(contacts);
  }

  [HttpPost("me/emergency-contacts")]
  public async Task<ActionResult<EmergencyContactResponseDto>> CreateMine(EmergencyContactCreateDto dto)
  {
    if (GetCurrentEmployeeId() is not int employeeId)
    {
      return Unauthorized();
    }

    var result = await _service.CreateAsync(employeeId, dto);
    return ToActionResult(result, StatusCodes.Status201Created);
  }

  [HttpPut("me/emergency-contacts/{contactId:int}")]
  public async Task<ActionResult<EmergencyContactResponseDto>> UpdateMine(int contactId, EmergencyContactCreateDto dto)
  {
    if (GetCurrentEmployeeId() is not int employeeId)
    {
      return Unauthorized();
    }

    var result = await _service.UpdateAsync(employeeId, contactId, dto);
    return ToActionResult(result);
  }

  [HttpDelete("me/emergency-contacts/{contactId:int}")]
  public async Task<IActionResult> DeleteMine(int contactId)
  {
    if (GetCurrentEmployeeId() is not int employeeId)
    {
      return Unauthorized();
    }

    return await _service.DeleteAsync(employeeId, contactId) ? NoContent() : NotFound();
  }

  [Authorize(Policy = PermissionNames.Employees.Read)]
  [HttpGet("{id:int}/emergency-contacts")]
  public async Task<ActionResult<List<EmergencyContactResponseDto>>> GetForEmployee(int id)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Read) is { } denied) return denied;

    var contacts = await _service.GetAllAsync(id);
    return contacts is null ? NotFound() : Ok(contacts);
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPost("{id:int}/emergency-contacts")]
  public async Task<ActionResult<EmergencyContactResponseDto>> CreateForEmployee(int id, EmergencyContactCreateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.CreateAsync(id, dto);
    return ToActionResult(result, StatusCodes.Status201Created);
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpPut("{id:int}/emergency-contacts/{contactId:int}")]
  public async Task<ActionResult<EmergencyContactResponseDto>> UpdateForEmployee(int id, int contactId, EmergencyContactCreateDto dto)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    var result = await _service.UpdateAsync(id, contactId, dto);
    return ToActionResult(result);
  }

  [Authorize(Policy = PermissionNames.Employees.Update)]
  [HttpDelete("{id:int}/emergency-contacts/{contactId:int}")]
  public async Task<IActionResult> DeleteForEmployee(int id, int contactId)
  {
    if (await _access.CheckAsync(User, id, PermissionNames.Employees.Update) is { } denied) return denied;

    return await _service.DeleteAsync(id, contactId) ? NoContent() : NotFound();
  }

  // Reads the logged-in employee's id from the JWT "sub" claim.
  private int? GetCurrentEmployeeId()
  {
    var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    return int.TryParse(sub, out var id) ? id : null;
  }

  // Translates a service outcome into an HTTP response.
  // Errors use { message } to match AuthController's convention.
  private ActionResult<EmergencyContactResponseDto> ToActionResult(
      ServiceResult<EmergencyContactResponseDto> result,
      int successStatusCode = StatusCodes.Status200OK)
  {
    return result.ErrorType switch
    {
      null => StatusCode(successStatusCode, result.Value),
      ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
      ServiceErrorType.Validation => BadRequest(new { message = result.ErrorMessage }),
      _ => throw new InvalidOperationException($"Unhandled service error type: {result.ErrorType}"),
    };
  }
}