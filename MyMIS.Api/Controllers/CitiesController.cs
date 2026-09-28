using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Services;
using System.Text.Json;

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

  [HttpPost("bulk")]
  [Authorize(Policy = "locations.manage")]
  public async Task<ActionResult<BulkInsertResultDto>> BulkCreate(
    [FromQuery] int? regionId,
    [FromBody] List<JsonElement> rows)
  {
    if (regionId is null)
    {
      return BadRequest("The regionId query parameter is required.");
    }

    if (BulkRowParser.GetRowCountError(rows.Count) is { } countError)
    {
      return BadRequest(countError);
    }

    var result = await _cityService.BulkCreateAsync(regionId.Value, rows);

    if (result is null)
    {
      return NotFound($"Region with id {regionId} was not found.");
    }

    return Ok(result);
  }
}