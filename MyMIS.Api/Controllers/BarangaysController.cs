using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BarangaysController(BarangayService barangayService) : ControllerBase
{
  private readonly BarangayService _barangayService = barangayService;

  [Authorize]
  [HttpGet]
  public async Task<ActionResult<List<BarangayResponseDto>>> GetAll([FromQuery] int? cityId)
  {
    return Ok(await _barangayService.GetAllAsync(cityId));
  }

  [Authorize]
  [HttpGet("{id}")]
  public async Task<ActionResult<BarangayResponseDto>> GetById(int id)
  {
    var barangay = await _barangayService.GetByIdAsync(id);
    if (barangay is null) return NotFound();
    return Ok(barangay);
  }

  [Authorize(Policy = "locations.manage")]
  [HttpPost]
  public async Task<ActionResult<BarangayResponseDto>> Create(BarangayCreateDto dto)
  {
    var created = await _barangayService.CreateAsync(dto);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
  }

  [Authorize(Policy = "locations.manage")]
  [HttpPut("{id}")]
  public async Task<ActionResult<BarangayResponseDto>> Update(int id, BarangayCreateDto dto)
  {
    var updated = await _barangayService.UpdateAsync(id, dto);
    if (updated is null) return NotFound();
    return Ok(updated);
  }

  [Authorize(Policy = "locations.manage")]
  [HttpDelete("{id}")]
  public async Task<IActionResult> Delete(int id)
  {
    var deleted = await _barangayService.DeleteAsync(id);
    if (!deleted) return NotFound();
    return NoContent();
  }
}