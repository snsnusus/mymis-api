using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController(RoleService service) : ApiControllerBase
{
  private readonly RoleService _service = service;

  [Authorize(Policy = PermissionNames.Roles.Read)]
  [HttpGet]
  public async Task<ActionResult<List<RoleSummaryResponseDto>>> GetAll()
  {
    return Ok(await _service.GetAllAsync());
  }

  [Authorize(Policy = PermissionNames.Roles.Read)]
  [HttpGet("{id:int}")]
  public async Task<ActionResult<RoleResponseDto>> GetById(int id)
  {
    var role = await _service.GetByIdAsync(id);
    return role is null ? NotFound() : Ok(role);
  }

  [Authorize(Policy = PermissionNames.Roles.Create)]
  [HttpPost]
  public async Task<ActionResult<RoleResponseDto>> Create(RoleCreateDto dto)
  {
    if (User.GetRoleId() is not int callerRoleId)
    {
      return Forbid();
    }

    var result = await _service.CreateAsync(dto, callerRoleId);
    return ToActionResult(result, value => CreatedAtAction(nameof(GetById), new { id = value.Id }, value));
  }

  [Authorize(Policy = PermissionNames.Roles.Update)]
  [HttpPut("{id:int}")]
  public async Task<ActionResult<RoleResponseDto>> Update(int id, RoleCreateDto dto)
  {
    if (User.GetRoleId() is not int callerRoleId)
    {
      return Forbid();
    }

    var result = await _service.UpdateAsync(id, dto, callerRoleId);
    return ToActionResult(result, Ok);
  }

  [Authorize(Policy = PermissionNames.Roles.Create)]
  [HttpPost("{id:int}/clone")]
  public async Task<ActionResult<RoleResponseDto>> Clone(int id, RoleCloneDto dto)
  {
    if (User.GetRoleId() is not int callerRoleId)
    {
      return Forbid();
    }

    var result = await _service.CloneAsync(id, dto, callerRoleId);
    return ToActionResult(result, value => CreatedAtAction(nameof(GetById), new { id = value.Id }, value));
  }

  [Authorize(Policy = PermissionNames.Roles.Delete)]
  [HttpDelete("{id:int}")]
  public async Task<IActionResult> Delete(int id)
  {
    var result = await _service.DeleteAsync(id);
    return ToActionResult(result, _ => NoContent());
  }
}