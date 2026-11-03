using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;
using MyMIS.Api.Services;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionsController : ControllerBase
{
  [HttpGet]
  [Authorize(Policy = PermissionNames.Roles.Read)]
  public ActionResult<List<PermissionResponseDto>> GetAll() =>
    Ok(RoleService.GetPermissionCatalog());
}