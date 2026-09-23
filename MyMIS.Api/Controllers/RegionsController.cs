using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegionsController(RegionService regionService) : ControllerBase
{
  private readonly RegionService _regionService = regionService;

  [Authorize]
  [HttpGet]
  public async Task<ActionResult<List<RegionResponseDto>>> GetAll()
  {
    return Ok(await _regionService.GetAllAsync());
  }

  [Authorize]
  [HttpGet("{id}")]
  public async Task<ActionResult<RegionResponseDto>> GetById(int id)
  {
    var region = await _regionService.GetByIdAsync(id);
    if (region is null) return NotFound();
    return Ok(region);
  }

  [Authorize(Policy = "locations.manage")]
  [HttpPost]
  public async Task<ActionResult<RegionResponseDto>> Create(RegionCreateDto dto)
  {
    var created = await _regionService.CreateAsync(dto);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
  }

  [Authorize(Policy = "locations.manage")]
  [HttpPut("{id}")]
  public async Task<ActionResult<RegionResponseDto>> Update(int id, RegionCreateDto dto)
  {
    var updated = await _regionService.UpdateAsync(id, dto);
    if (updated is null) return NotFound();
    return Ok(updated);
  }

  [Authorize(Policy = "locations.manage")]
  [HttpDelete("{id}")]
  public async Task<IActionResult> Delete(int id)
  {
    var deleted = await _regionService.DeleteAsync(id);
    if (!deleted) return NotFound();
    return NoContent();
  }
}