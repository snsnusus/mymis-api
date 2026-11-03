using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OfficesController(OfficeService officeService) : ControllerBase
{
  private readonly OfficeService _officeService = officeService;

  [Authorize(Policy = PermissionNames.Offices.Read)]
  [HttpGet]
  public async Task<ActionResult<List<OfficeResponseDto>>> GetAll()
  {
    return Ok(await _officeService.GetAllAsync());
  }

  [Authorize(Policy = PermissionNames.Offices.Read)]
  [HttpGet("{id:int}")]
  public async Task<ActionResult<OfficeResponseDto>> GetById(int id)
  {
    var office = await _officeService.GetByIdAsync(id);
    if (office is null) return NotFound();
    return Ok(office);
  }

  [Authorize(Policy = PermissionNames.Offices.Create)]
  [HttpPost]
  public async Task<ActionResult<OfficeResponseDto>> Create(OfficeCreateDto dto)
  {
    if (await _officeService.NameExistsAsync(dto.Name))
    {
      return Conflict($"An office named '{dto.Name.Trim()}' already exists.");
    }

    var created = await _officeService.CreateAsync(dto);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
  }

  [Authorize(Policy = PermissionNames.Offices.Update)]
  [HttpPut("{id:int}")]
  public async Task<ActionResult<OfficeResponseDto>> Update(int id, OfficeCreateDto dto)
  {
    if (await _officeService.GetByIdAsync(id) is null) return NotFound();

    if (await _officeService.NameExistsAsync(dto.Name, excludeId: id))
    {
      return Conflict($"An office named '{dto.Name.Trim()}' already exists.");
    }

    var updated = await _officeService.UpdateAsync(id, dto);
    if (updated is null) return NotFound();
    return Ok(updated);
  }

  [Authorize(Policy = PermissionNames.Offices.Delete)]
  [HttpDelete("{id:int}")]
  public async Task<IActionResult> Delete(int id)
  {
    var deleted = await _officeService.DeleteAsync(id);
    if (!deleted) return NotFound();
    return NoContent();
  }
}