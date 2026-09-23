using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CitiesController(CityService cityService) : ControllerBase
{
  private readonly CityService _cityService = cityService;

  [Authorize]
  [HttpGet]
  public async Task<ActionResult<List<CityResponseDto>>> GetAll([FromQuery] int? regionId)
  {
    return Ok(await _cityService.GetAllAsync(regionId));
  }

  [Authorize]
  [HttpGet("{id}")]
  public async Task<ActionResult<CityResponseDto>> GetById(int id)
  {
    var city = await _cityService.GetByIdAsync(id);
    if (city is null) return NotFound();
    return Ok(city);
  }

  [Authorize(Policy = "locations.manage")]
  [HttpPost]
  public async Task<ActionResult<CityResponseDto>> Create(CityCreateDto dto)
  {
    var created = await _cityService.CreateAsync(dto);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
  }

  [Authorize(Policy = "locations.manage")]
  [HttpPut("{id}")]
  public async Task<ActionResult<CityResponseDto>> Update(int id, CityCreateDto dto)
  {
    var updated = await _cityService.UpdateAsync(id, dto);
    if (updated is null) return NotFound();
    return Ok(updated);
  }

  [Authorize(Policy = "locations.manage")]
  [HttpDelete("{id}")]
  public async Task<IActionResult> Delete(int id)
  {
    var deleted = await _cityService.DeleteAsync(id);
    if (!deleted) return NotFound();
    return NoContent();
  }
}