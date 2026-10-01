using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.Dtos;
using MyMIS.Api.Helpers;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/Employees")]
[Authorize]
public class EmergencyContactsController : ControllerBase
{
  private readonly EmergencyContactService _service;

  public EmergencyContactsController(EmergencyContactService service)
  {
    _service = service;
  }

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

  [HttpGet("{id:int}/emergency-contacts")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<List<EmergencyContactResponseDto>>> GetForEmployee(int id)
  {
    var contacts = await _service.GetAllAsync(id);
    return contacts is null ? NotFound() : Ok(contacts);
  }

  [HttpPost("{id:int}/emergency-contacts")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<EmergencyContactResponseDto>> CreateForEmployee(int id, EmergencyContactCreateDto dto)
  {
    var result = await _service.CreateAsync(id, dto);
    return ToActionResult(result, StatusCodes.Status201Created);
  }

  [HttpPut("{id:int}/emergency-contacts/{contactId:int}")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<EmergencyContactResponseDto>> UpdateForEmployee(int id, int contactId, EmergencyContactCreateDto dto)
  {
    var result = await _service.UpdateAsync(id, contactId, dto);
    return ToActionResult(result);
  }

  [HttpDelete("{id:int}/emergency-contacts/{contactId:int}")]
  [Authorize(Policy = "employees.update")]
  public async Task<IActionResult> DeleteForEmployee(int id, int contactId)
  {
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