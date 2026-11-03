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
public class RegionsController(RegionService regionService) : ControllerBase
{
  private readonly RegionService _regionService = regionService;

  [Authorize(Policy = PermissionNames.Locations.Read)]
  [HttpGet]
  public async Task<ActionResult<List<RegionResponseDto>>> GetAll()
  {
    return Ok(await _regionService.GetAllAsync());
  }

  [Authorize(Policy = PermissionNames.Locations.Read)]
  [HttpGet("{id}")]
  public async Task<ActionResult<RegionResponseDto>> GetById(int id)
  {
    var region = await _regionService.GetByIdAsync(id);
    if (region is null) return NotFound();
    return Ok(region);
  }

  [Authorize(Policy = PermissionNames.Locations.Create)]
  [HttpPost]
  public async Task<ActionResult<RegionResponseDto>> Create(RegionCreateDto dto)
  {
    var created = await _regionService.CreateAsync(dto);
    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
  }

  [Authorize(Policy = PermissionNames.Locations.Update)]
  [HttpPut("{id}")]
  public async Task<ActionResult<RegionResponseDto>> Update(int id, RegionCreateDto dto)
  {
    var updated = await _regionService.UpdateAsync(id, dto);
    if (updated is null) return NotFound();
    return Ok(updated);
  }

  [Authorize(Policy = PermissionNames.Locations.Delete)]
  [HttpDelete("{id}")]
  public async Task<IActionResult> Delete(int id)
  {
    var deleted = await _regionService.DeleteAsync(id);
    if (!deleted) return NotFound();
    return NoContent();
  }

  [Authorize(Policy = PermissionNames.Locations.Create)]
  [HttpPost("bulk")]
  public async Task<ActionResult<BulkInsertResultDto>> BulkCreate([FromBody] List<JsonElement> rows)
  {
    if (BulkRowParser.GetRowCountError(rows.Count) is { } countError)
    {
      return BadRequest(countError);
    }

    var result = await _regionService.BulkCreateAsync(rows);
    return Ok(result);
  }
}