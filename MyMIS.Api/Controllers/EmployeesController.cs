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
    public async Task<ActionResult<List<EmployeeSummaryResponseDto>>> GetAll()
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

    [Authorize(Policy = "employees.create")]
    [HttpPost]
    public async Task<ActionResult<EmployeeResponseDto>> Create(EmployeeCreateDto dto)
    {
        var created = await _employeeService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = "employees.update")]
    [HttpPut("{id}")]
    public async Task<ActionResult<EmployeeResponseDto>> Update(int id, EmployeeUpdateDto dto)
    {
        var updated = await _employeeService.UpdateAsync(id, dto);
        if (updated is null) return NotFound();
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

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPut("partial/{id}")]
    public async Task<ActionResult<EmployeeResponseDto>> UpdatePartial(int id, EmployeePartialUpdateDto dto)
    {
        var (exists, currentDepartmentId) = await _employeeService.GetExistenceAndDepartmentAsync(id);
        if (!exists) return NotFound();

        var targetDepartmentId = currentDepartmentId ?? -1;

        var authResult = await _authorizationService.AuthorizeAsync(User, targetDepartmentId, "DepartmentScope");
        if (!authResult.Succeeded) return Forbid();

        var updated = await _employeeService.UpdatePartialAsync(id, dto);
        return Ok(updated);
    }

    [Authorize]
    [HttpPost("me/hobbies")]
    public async Task<ActionResult<HobbyResponseDto>> LinkHobbyMe(HobbyCreateDto dto)
    {
        var employeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (employeeIdClaim is null || !int.TryParse(employeeIdClaim, out var employeeId))
        {
            return Unauthorized();
        }

        var result = await _employeeService.LinkHobbyAsync(employeeId, dto.Name);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [Authorize]
    [HttpDelete("me/hobbies/{hobbyId}")]
    public async Task<IActionResult> UnlinkHobbyMe(int hobbyId)
    {
        var employeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (employeeIdClaim is null || !int.TryParse(employeeIdClaim, out var employeeId))
        {
            return Unauthorized();
        }

        var removed = await _employeeService.UnlinkHobbyAsync(employeeId, hobbyId);

        if (!removed)
        {
            return NotFound();
        }

        return NoContent();
    }
}