using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DepartmentsController(DepartmentService departmentService, IAuthorizationService authorizationService) : ControllerBase
{
  private readonly DepartmentService _departmentService = departmentService;
  private readonly IAuthorizationService _authorizationService = authorizationService;

  [Authorize(Policy = PermissionNames.Departments.Read)]
  [HttpGet]
  public async Task<ActionResult<List<DepartmentSummaryResponseDto>>> GetAll()
  {
    return Ok(await _departmentService.GetAllAsync());
  }

  [Authorize(Policy = PermissionNames.Departments.Read)]
  [HttpGet("{id}")]
  public async Task<ActionResult<DepartmentResponseDto>> GetById(int id)
  {
    var department = await _departmentService.GetByIdAsync(id);
    if (department is null) return NotFound();
    return Ok(department);
  }

  [Authorize(Policy = PermissionNames.Departments.Create)]
  [HttpPost]
  public async Task<ActionResult<DepartmentResponseDto>> Create(DepartmentCreateDto dto)
  {
    var created = await _departmentService.CreateAsync(dto);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
  }

  [Authorize(Policy = PermissionNames.Departments.Update)]
  [HttpPut("{id}")]
  public async Task<ActionResult<DepartmentResponseDto>> Update(int id, DepartmentCreateDto dto)
  {
    var existing = await _departmentService.GetByIdAsync(id);
    if (existing is null) return NotFound();

    var authResult = await _authorizationService.AuthorizeAsync(User, id, PermissionNames.Departments.Update);
    if (!authResult.Succeeded) return Forbid();

    var updated = await _departmentService.UpdateAsync(id, dto);
    return Ok(updated);
  }

  [Authorize(Policy = PermissionNames.Departments.Delete)]
  [HttpDelete("{id}")]
  public async Task<IActionResult> Delete(int id)
  {
    var deleted = await _departmentService.DeleteAsync(id);
    if (!deleted) return NotFound();
    return NoContent();
  }
}