using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "hmo.manage")]
public class HmoProvidersController(HmoProviderService service) : ControllerBase
{
  private readonly HmoProviderService _service = service;

  [HttpGet]
  public async Task<ActionResult<List<HmoProviderResponseDto>>> GetAll()
  {
    return Ok(await _service.GetAllAsync());
  }

  [HttpGet("{id:int}")]
  public async Task<ActionResult<HmoProviderResponseDto>> GetById(int id)
  {
    var provider = await _service.GetByIdAsync(id);
    if (provider is null)
    {
      return NotFound();
    }

    return Ok(provider);
  }

  [HttpPost]
  public async Task<ActionResult<HmoProviderResponseDto>> Create(HmoProviderCreateDto dto)
  {
    if (await _service.CodeExistsAsync(dto.Code))
    {
      return Conflict(new { message = "An HMO provider with this code already exists." });
    }

    if (await _service.NameExistsAsync(dto.Name))
    {
      return Conflict(new { message = "An HMO provider with this name already exists." });
    }

    var created = await _service.CreateAsync(dto);

    return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
  }

  [HttpPut("{id:int}")]
  public async Task<ActionResult<HmoProviderResponseDto>> Update(int id, HmoProviderCreateDto dto)
  {
    if (await _service.GetByIdAsync(id) is null)
    {
      return NotFound();
    }

    if (await _service.CodeExistsAsync(dto.Code, excludeId: id))
    {
      return Conflict(new { message = "An HMO provider with this code already exists." });
    }

    if (await _service.NameExistsAsync(dto.Name, excludeId: id))
    {
      return Conflict(new { message = "An HMO provider with this name already exists." });
    }

    var updated = await _service.UpdateAsync(id, dto);
    if (updated is null)
    {
      return NotFound();
    }

    return Ok(updated);
  }
}