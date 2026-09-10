using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Services;
using System.IdentityModel.Tokens.Jwt;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController(EmployeeService employeeService, IAuthorizationService authorizationService) : ControllerBase
{
    private readonly EmployeeService _employeeService = employeeService;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<EmployeeResponseDto>>> GetAll()
    {
        var employees = await _employeeService.GetAllAsync();
        return Ok(employees);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<ActionResult<EmployeeResponseDto>> GetById(int id)
    {
        var employee = await _employeeService.GetByIdAsync(id);
        if (employee is null) return NotFound();
        return Ok(employee);
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost]
    public async Task<ActionResult<EmployeeResponseDto>> Create(EmployeeCreateDto dto)
    {
        var created = await _employeeService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<EmployeeResponseDto>> Update(int id, EmployeeUpdateDto dto)
    {
        var (exists, currentDepartmentId) = await _employeeService.GetExistenceAndDepartmentAsync(id);
        if (!exists) return NotFound();

        var targetDepartmentId = currentDepartmentId ?? -1;

        var authResult = await _authorizationService.AuthorizeAsync(User, targetDepartmentId, "DepartmentScope");
        if (!authResult.Succeeded) return Forbid();

        var updated = await _employeeService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _employeeService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<EmployeeResponseDto>> UpdateMe(EmployeeSelfUpdateDto dto)
    {
        var employeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (employeeIdClaim is null || !int.TryParse(employeeIdClaim, out var employeeId))
        {
            return Unauthorized();
        }

        var result = await _employeeService.UpdateSelfAsync(employeeId, dto);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}