using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Services;
using System.Text.Json;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BarangaysController(BarangayService barangayService) : ControllerBase
{
  private readonly BarangayService _barangayService = barangayService;

  [Authorize(Policy = PermissionNames.Locations.Read)]
  [HttpGet]
  public async Task<ActionResult<List<BarangayResponseDto>>> GetAll([FromQuery] int? cityId)
  {
    return Ok(await _barangayService.GetAllAsync(cityId));
  }

  [Authorize(Policy = PermissionNames.Locations.Read)]
  [HttpGet("{id}")]
  public async Task<ActionResult<BarangayResponseDto>> GetById(int id)
  {
    var barangay = await _barangayService.GetByIdAsync(id);
    if (barangay is null) return NotFound();
    return Ok(barangay);
  }

  [Authorize(Policy = PermissionNames.Locations.Create)]
  [HttpPost]
  public async Task<ActionResult<BarangayResponseDto>> Create(BarangayCreateDto dto)
  {
    var created = await _barangayService.CreateAsync(dto);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
  }

  [Authorize(Policy = PermissionNames.Locations.Update)]
  [HttpPut("{id}")]
  public async Task<ActionResult<BarangayResponseDto>> Update(int id, BarangayCreateDto dto)
  {
    var updated = await _barangayService.UpdateAsync(id, dto);
    if (updated is null) return NotFound();
    return Ok(updated);
  }

  [Authorize(Policy = PermissionNames.Locations.Delete)]
  [HttpDelete("{id}")]
  public async Task<IActionResult> Delete(int id)
  {
    var deleted = await _barangayService.DeleteAsync(id);
    if (!deleted) return NotFound();
    return NoContent();
  }

  [Authorize(Policy = PermissionNames.Locations.Create)]
  [HttpPost("bulk")]
  public async Task<ActionResult<BulkInsertResultDto>> BulkCreate(
    [FromQuery] int? cityId,
    [FromBody] List<JsonElement> rows)
  {
    if (cityId is null)
    {
      return BadRequest("The cityId query parameter is required.");
    }

    if (BulkRowParser.GetRowCountError(rows.Count) is { } countError)
    {
      return BadRequest(countError);
    }

    var result = await _barangayService.BulkCreateAsync(cityId.Value, rows);

    if (result is null)
    {
      return NotFound($"City with id {cityId} was not found.");
    }

    return Ok(result);
  }
}