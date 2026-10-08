using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/Employees")]
[Authorize]
public class EmployeeEmailsController(EmployeeEmailService service) : ApiControllerBase
{
  private readonly EmployeeEmailService _service = service;

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

  [HttpGet("{id:int}/emails")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<List<EmployeeEmailResponseDto>>> GetForEmployee(int id)
  {
    var emails = await _service.GetAllAsync(id);
    return emails is null ? NotFound() : Ok(emails);
  }

  [HttpGet("{id:int}/emails/{emailId:int}")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<EmployeeEmailResponseDto>> GetByIdForEmployee(int id, int emailId)
  {
    var email = await _service.GetByIdAsync(id, emailId);
    return email is null ? NotFound() : Ok(email);
  }

  [HttpPost("{id:int}/emails")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<EmployeeEmailResponseDto>> CreateForEmployee(int id, EmployeeEmailCreateDto dto)
  {
    var result = await _service.CreateAsync(id, dto);
    return ToActionResult(result, value => StatusCode(StatusCodes.Status201Created, value));
  }

  [HttpPut("{id:int}/emails/{emailId:int}")]
  [Authorize(Policy = "employees.update")]
  public async Task<ActionResult<EmployeeEmailResponseDto>> UpdateForEmployee(int id, int emailId, EmployeeEmailCreateDto dto)
  {
    var result = await _service.UpdateAsync(id, emailId, dto);
    return ToActionResult(result, Ok);
  }

  [HttpDelete("{id:int}/emails/{emailId:int}")]
  [Authorize(Policy = "employees.update")]
  public async Task<IActionResult> DeleteForEmployee(int id, int emailId)
  {
    var result = await _service.DeleteAsync(id, emailId);
    return ToActionResult(result, _ => NoContent());
  }
}