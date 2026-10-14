using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HmoPlansController(HmoPlanService service) : ControllerBase
{
  private readonly HmoPlanService _service = service;

  [HttpGet]
  [Authorize(Policy = "hmo.manage")]
  public async Task<ActionResult<List<HmoPlanSummaryDto>>> GetByProvider(
      [FromQuery, BindRequired] int providerId)
  {
    var plans = await _service.GetByProviderAsync(providerId);
    if (plans is null)
    {
      return NotFound(new { message = "HMO provider not found." });
    }

    return Ok(plans);
  }

  [HttpGet("lookup")]
  public async Task<ActionResult<List<HmoPlanLookupDto>>> GetLookup(
      [FromQuery, BindRequired] int providerId)
  {
    return Ok(await _service.GetLookupAsync(providerId));
  }

  [HttpGet("{id:int}")]
  [Authorize(Policy = "hmo.manage")]
  public async Task<ActionResult<HmoPlanResponseDto>> GetById(int id)
  {
    var plan = await _service.GetByIdAsync(id);
    if (plan is null)
    {
      return NotFound();
    }

    return Ok(plan);
  }

  [HttpPost]
  [Authorize(Policy = "hmo.manage")]
  public async Task<ActionResult<HmoPlanResponseDto>> Create(HmoPlanCreateDto dto)
  {
    if (await _service.NameExistsAsync(dto.HmoProviderId!.Value, dto.Name))
    {
      return Conflict(new { message = "This HMO provider already has a plan with this name." });
    }

    var result = await _service.CreateAsync(dto);
    if (!result.IsSuccess)
    {
      return ToErrorResult(result);
    }

    return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
  }

  [HttpPut("{id:int}")]
  [Authorize(Policy = "hmo.manage")]
  public async Task<ActionResult<HmoPlanResponseDto>> Update(int id, HmoPlanCreateDto dto)
  {
    if (!await _service.ExistsAsync(id))
    {
      return NotFound();
    }

    if (await _service.NameExistsAsync(dto.HmoProviderId!.Value, dto.Name, excludeId: id))
    {
      return Conflict(new { message = "This HMO provider already has a plan with this name." });
    }

    var result = await _service.UpdateAsync(id, dto);
    if (!result.IsSuccess)
    {
      return ToErrorResult(result);
    }

    return Ok(result.Value);
  }

  private ActionResult ToErrorResult<T>(ServiceResult<T> result) => result.ErrorType switch
  {
    ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
    _ => BadRequest(new { message = result.ErrorMessage })
  };
}