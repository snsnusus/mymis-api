using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DepartmentsController(DepartmentService departmentService, IAuthorizationService authorizationService) : ControllerBase
{
    private readonly DepartmentService _departmentService = departmentService;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<DepartmentResponseDto>>> GetAll()
    {
        return Ok(await _departmentService.GetAllAsync());
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<ActionResult<DepartmentResponseDto>> GetById(int id)
    {
        var department = await _departmentService.GetByIdAsync(id);
        if (department is null) return NotFound();
        return Ok(department);
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost]
    public async Task<ActionResult<DepartmentResponseDto>> Create(DepartmentCreateDto dto)
    {
        var created = await _departmentService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<DepartmentResponseDto>> Update(int id, DepartmentCreateDto dto)
    {
        var existing = await _departmentService.GetByIdAsync(id);
        if (existing is null) return NotFound();

        var authResult = await _authorizationService.AuthorizeAsync(User, id, "DepartmentScope");
        if (!authResult.Succeeded) return Forbid();

        var updated = await _departmentService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _departmentService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}