using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PositionsController(PositionService positionService, IAuthorizationService authorizationService) : ControllerBase
{
    private readonly PositionService _positionService = positionService;
    private readonly IAuthorizationService _authorizationService = authorizationService;

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<PositionResponseDto>>> GetAll()
    {
        return Ok(await _positionService.GetAllAsync());
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<ActionResult<PositionResponseDto>> GetById(int id)
    {
        var position = await _positionService.GetByIdAsync(id);
        if (position is null) return NotFound();
        return Ok(position);
    }

    [Authorize(Policy = "positions.create")]
    [HttpPost]
    public async Task<ActionResult<PositionResponseDto>> Create(PositionCreateDto dto)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, dto.DepartmentId, "SameDepartment");
        if (!authResult.Succeeded) return Forbid();

        var created = await _positionService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = "positions.update")]
    [HttpPut("{id}")]
    public async Task<ActionResult<PositionResponseDto>> Update(int id, PositionCreateDto dto)
    {
        var existing = await _positionService.GetByIdAsync(id);
        if (existing is null) return NotFound();

        var authResult = await _authorizationService.AuthorizeAsync(User, existing.DepartmentId, "SameDepartment");
        if (!authResult.Succeeded) return Forbid();

        var updated = await _positionService.UpdateAsync(id, dto);
        return Ok(updated);
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _positionService.DeleteAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
