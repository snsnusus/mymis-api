using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PositionsController(PositionService positionService, IAuthorizationService authorizationService) : ControllerBase
{
  private readonly PositionService _positionService = positionService;
  private readonly IAuthorizationService _authorizationService = authorizationService;

  [Authorize(Policy = PermissionNames.Positions.Read)]
  [HttpGet]
  public async Task<ActionResult<List<PositionResponseDto>>> GetAll([FromQuery] int? departmentId)
  {
    return Ok(await _positionService.GetAllAsync(departmentId));
  }

  [Authorize(Policy = PermissionNames.Positions.Read)]
  [HttpGet("{id}")]
  public async Task<ActionResult<PositionResponseDto>> GetById(int id)
  {
    var position = await _positionService.GetByIdAsync(id);
    if (position is null) return NotFound();
    return Ok(position);
  }

  [Authorize(Policy = PermissionNames.Positions.Create)]
  [HttpPost]
  public async Task<ActionResult<PositionResponseDto>> Create(PositionCreateDto dto)
  {
    var authResult = await _authorizationService.AuthorizeAsync(User, dto.DepartmentId, PermissionNames.Positions.Create);
    if (!authResult.Succeeded) return Forbid();

    var created = await _positionService.CreateAsync(dto);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
  }

  [Authorize(Policy = PermissionNames.Positions.Update)]
  [HttpPut("{id}")]
  public async Task<ActionResult<PositionResponseDto>> Update(int id, PositionCreateDto dto)
  {
    var existing = await _positionService.GetByIdAsync(id);
    if (existing is null) return NotFound();

    // Gate 2, part 1: the department the position belongs to NOW.
    var current = await _authorizationService.AuthorizeAsync(
      User, existing.DepartmentId, PermissionNames.Positions.Update);
    if (!current.Succeeded) return Forbid();

    // Gate 2, part 2: if this update MOVES the position, the caller must also
    // cover the department it's moving INTO. Closes the gap noted in
    // Design-Notes_Position-SameDepartment-Authorization.md §8: without this,
    // a department-scoped caller could push a position into another department.
    // Company-scoped callers (HR) pass both checks automatically.
    if (dto.DepartmentId != existing.DepartmentId)
    {
      var destination = await _authorizationService.AuthorizeAsync(
        User, dto.DepartmentId, PermissionNames.Positions.Update);
      if (!destination.Succeeded) return Forbid();
    }

    var updated = await _positionService.UpdateAsync(id, dto);
    return Ok(updated);
  }

  [Authorize(Policy = PermissionNames.Positions.Delete)]
  [HttpDelete("{id}")]
  public async Task<IActionResult> Delete(int id)
  {
    var deleted = await _positionService.DeleteAsync(id);
    if (!deleted) return NotFound();
    return NoContent();
  }
}
