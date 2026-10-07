using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.DTOs;
using MyMIS.Api.Exceptions;
using MyMIS.Api.Helpers;
using MyMIS.Api.Services;
using MyMIS.Api.Validation;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;

namespace MyMIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController(
  EmployeeService employeeService,
  EmergencyContactService emergencyContactService,
  EmployeeAddressService employeeAddressService,
  EmployeePhoneService employeePhoneService,
  IAuthorizationService authorizationService
) : ControllerBase
{
  private readonly EmployeeService _employeeService = employeeService;
  private readonly EmergencyContactService _emergencyContactService = emergencyContactService;
  private readonly EmployeeAddressService _employeeAddressService = employeeAddressService;
  private readonly EmployeePhoneService _employeePhoneService = employeePhoneService;
  private readonly IAuthorizationService _authorizationService = authorizationService;

  [Authorize]
  [HttpGet]
  public async Task<ActionResult<PagedResult<EmployeeSummaryResponseDto>>> GetAll(
  [FromQuery] string? search,
  [FromQuery] int page = 1,
  [FromQuery] int pageSize = 20)
  {
    var safePageSize = Math.Clamp(pageSize, 1, 100);

    // The upper bound keeps (page - 1) * pageSize from overflowing int.
    var safePage = Math.Clamp(page, 1, int.MaxValue / safePageSize);

    var result = await _employeeService.GetPagedAsync(search, safePage, safePageSize);
    return Ok(result);
  }

  [Authorize]
  [HttpGet("lookup")]
  public async Task<ActionResult<List<EmployeeLookupDto>>> GetLookup(
  [FromQuery] string? search,
  [FromQuery] int limit = 20)
  {
    // Never let the client ask for an unbounded result. 1 to 50 rows.
    var safeLimit = Math.Clamp(limit, 1, 50);

    var results = await _employeeService.GetLookupAsync(search, safeLimit);
    return Ok(results);
  }

  [Authorize(Policy = "employees.create")]
  [HttpGet("availability")]
  public async Task<ActionResult<UsernameAvailabilityResponseDto>> CheckAvailability(
  [FromQuery, Required, ValidUsername] string username)
  {
    var available = await _employeeService.IsUsernameAvailableAsync(username);
    return Ok(new UsernameAvailabilityResponseDto { Available = available });
  }

  [Authorize]
  [HttpGet("{id}")]
  public async Task<ActionResult<EmployeeResponseDto>> GetById(int id)
  {
    var canSeeContacts = await CanSeeEmergencyContactsAsync(id);
    var employee = await _employeeService.GetByIdAsync(id, includeEmergencyContacts: canSeeContacts);

    if (employee is null) return NotFound();
    return Ok(employee);
  }

  [Authorize(Policy = "employees.create")]
  [HttpPost]
  public async Task<ActionResult<EmployeeResponseDto>> Create(EmployeeCreateDto dto)
  {
    var (_, contactError) = await _emergencyContactService.ValidateAsync(dto.EmergencyContact!);
    if (contactError is not null)
    {
      ModelState.AddModelError(nameof(dto.EmergencyContact), contactError);
      return ValidationProblem(ModelState);
    }

    var addressError = await _employeeAddressService.ValidateForNewEmployeeAsync(dto.Addresses!);
    if (addressError is not null)
    {
      ModelState.AddModelError(nameof(dto.Addresses), addressError);
      return ValidationProblem(ModelState);
    }

    var phoneCheck = await _employeePhoneService.ValidateForNewEmployeeAsync(dto.Phones!);
    if (phoneCheck.ErrorType == ServiceErrorType.Validation)
    {
      ModelState.AddModelError(nameof(dto.Phones), phoneCheck.ErrorMessage!);
      return ValidationProblem(ModelState);
    }

    if (phoneCheck.ErrorType == ServiceErrorType.Conflict)
    {
      return PhonesConflict(phoneCheck.ErrorMessage!);
    }

    if (!await _employeeService.IsUsernameAvailableAsync(dto.Username))
    {
      return UsernameTakenConflict();
    }

    try
    {
      var created = await _employeeService.CreateAsync(dto);
      return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
    catch (DuplicateUsernameException)
    {
      return UsernameTakenConflict();
    }
  }

  [Authorize(Policy = "employees.update")]
  [HttpPut("{id}")]
  public async Task<ActionResult<EmployeeResponseDto>> Update(int id, EmployeeUpdateDto dto)
  {
    var canSeeContacts = await CanSeeEmergencyContactsAsync(id);
    var employee = await _employeeService.GetByIdAsync(id, includeEmergencyContacts: canSeeContacts);

    if (employee is null)
    {
      return NotFound();
    }

    if (!await _employeeService.IsUsernameAvailableAsync(dto.Username, excludeEmployeeId: id))
    {
      return UsernameTakenConflict();
    }

    try
    {
      var updated = await _employeeService.UpdateAsync(id, dto);
      if (updated is null) return NotFound();
      return Ok(updated);
    }
    catch (DuplicateUsernameException)
    {
      return UsernameTakenConflict();
    }
  }

  [Authorize(Roles = "SuperAdmin")]
  [HttpDelete("{id}")]
  public async Task<IActionResult> Delete(int id)
  {
    var deleted = await _employeeService.DeleteAsync(id);
    if (!deleted) return NotFound();
    return NoContent();
  }

  [Authorize]
  [HttpPut("me")]
  public async Task<ActionResult<EmployeeResponseDto>> UpdateMe(EmployeeSelfUpdateDto dto)
  {
    var employeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (employeeIdClaim is null || !int.TryParse(employeeIdClaim, out var employeeId))
    {
      return Unauthorized();
    }

    var result = await _employeeService.UpdateSelfAsync(employeeId, dto);

    if (result is null)
    {
      return NotFound();
    }

    return Ok(result);
  }

  [Authorize]
  [HttpPut("me/avatar-style")]
  public async Task<IActionResult> UpdateAvatarStyleMe(EmployeeAvatarStyleUpdateDto dto)
  {
    var employeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (employeeIdClaim is null || !int.TryParse(employeeIdClaim, out var employeeId))
    {
      return Unauthorized();
    }

    // [Required] has already rejected a missing value with a 400 by this point.
    var updated = await _employeeService.UpdateAvatarStyleAsync(employeeId, dto.AvatarStyle!.Value);

    if (!updated)
    {
      return NotFound();
    }

    return NoContent();
  }

  [Authorize(Roles = "Admin,SuperAdmin")]
  [HttpPut("partial/{id}")]
  public async Task<ActionResult<EmployeeResponseDto>> UpdatePartial(int id, EmployeePartialUpdateDto dto)
  {
    var (exists, currentDepartmentId) = await _employeeService.GetExistenceAndDepartmentAsync(id);
    if (!exists) return NotFound();

    var targetDepartmentId = currentDepartmentId ?? -1;

    var authResult = await _authorizationService.AuthorizeAsync(User, targetDepartmentId, "DepartmentScope");
    if (!authResult.Succeeded) return Forbid();

    var updated = await _employeeService.UpdatePartialAsync(id, dto);
    return Ok(updated);
  }

  [Authorize]
  [HttpPost("me/hobbies")]
  public async Task<ActionResult<HobbyResponseDto>> LinkHobbyMe(HobbyCreateDto dto)
  {
    var employeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (employeeIdClaim is null || !int.TryParse(employeeIdClaim, out var employeeId))
    {
      return Unauthorized();
    }

    var result = await _employeeService.LinkHobbyAsync(employeeId, dto.Name);

    if (result is null)
    {
      return NotFound();
    }

    return Ok(result);
  }

  [Authorize]
  [HttpDelete("me/hobbies/{hobbyId}")]
  public async Task<IActionResult> UnlinkHobbyMe(int hobbyId)
  {
    var employeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (employeeIdClaim is null || !int.TryParse(employeeIdClaim, out var employeeId))
    {
      return Unauthorized();
    }

    var removed = await _employeeService.UnlinkHobbyAsync(employeeId, hobbyId);

    if (!removed)
    {
      return NotFound();
    }

    return NoContent();
  }

  [Authorize]
  [HttpPost("me/avatar")]
  [RequestSizeLimit(5 * 1024 * 1024)]
  public async Task<IActionResult> UploadAvatarMe(IFormFile file)
  {
    var employeeIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (employeeIdClaim is null || !int.TryParse(employeeIdClaim, out var employeeId))
    {
      return Unauthorized();
    }

    if (file.Length == 0)
    {
      return BadRequest("No file was uploaded.");
    }

    try
    {
      var avatarUrl = await _employeeService.UpdateAvatarAsync(employeeId, file);

      if (avatarUrl is null) return NotFound();

      return Ok(new AvatarUploadResponseDto { AvatarUrl = avatarUrl });
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(ex.Message);
    }
  }

  [Authorize]
  [HttpPost("{id}/avatar")]
  [RequestSizeLimit(5 * 1024 * 1024)]
  public async Task<IActionResult> UploadAvatar(int id, IFormFile file)
  {
    var canUpdate = (await _authorizationService.AuthorizeAsync(User, "employees.update")).Succeeded;
    var canCreate = (await _authorizationService.AuthorizeAsync(User, "employees.create")).Succeeded;

    if (!canUpdate && !canCreate)
    {
      return Forbid();
    }

    if (file.Length == 0)
    {
      return BadRequest("No file was uploaded.");
    }

    try
    {
      var avatarUrl = await _employeeService.UpdateAvatarAsync(id, file);

      if (avatarUrl is null) return NotFound();

      return Ok(new AvatarUploadResponseDto { AvatarUrl = avatarUrl });
    }
    catch (InvalidOperationException ex)
    {
      return BadRequest(ex.Message);
    }
  }

  [HttpPatch("{id}/avatar-thumbnail")]
  public async Task<IActionResult> UpdateAvatarThumbnail(
    int id,
    AvatarThumbnailUpdateResponseDto dto,
    [FromServices] IConfiguration configuration)
  {
    var providedSecret = Request.Headers["X-Internal-Secret"].FirstOrDefault();
    var expectedSecret = configuration["Internal:CallbackSecret"];

    if (!SecretsMatch(providedSecret, expectedSecret))
    {
      return Unauthorized();
    }

    var updated = await _employeeService.UpdateAvatarThumbnailAsync(id, dto.ThumbnailKey);
    if (!updated) return NotFound();

    return NoContent();
  }

  private static bool SecretsMatch(string? provided, string? expected)
  {
    if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
    {
      return false;
    }

    var providedBytes = Encoding.UTF8.GetBytes(provided);
    var expectedBytes = Encoding.UTF8.GetBytes(expected);

    return providedBytes.Length == expectedBytes.Length
        && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
  }

  // 409 in the same shape as a validation error, so the portal can show it
  // under the Username field exactly like a 400 from [ValidUsername].
  private ConflictObjectResult UsernameTakenConflict()
  {
    var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
    {
      ["Username"] = [DuplicateUsernameException.DefaultMessage],
    })
    {
      Status = StatusCodes.Status409Conflict,
      Title = "Username already taken.",
    };

    return Conflict(problem);
  }

  // 409 in validation-error shape so the portal can show it under the Phones section.
  private ConflictObjectResult PhonesConflict(string message)
  {
    var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
    {
      ["Phones"] = [message],
    })
    {
      Status = StatusCodes.Status409Conflict,
      Title = "Phone number already in use.",
    };

    return Conflict(problem);
  }

  // Emergency contacts are visible to the employee themselves and to anyone
  // who can create or update employees. SuperAdmin passes via PermissionHandler.
  private async Task<bool> CanSeeEmergencyContactsAsync(int employeeId)
  {
    if (GetCurrentEmployeeId() == employeeId)
    {
      return true;
    }

    foreach (var policy in new[] { "employees.update", "employees.create" })
    {
      var result = await _authorizationService.AuthorizeAsync(User, policy);
      if (result.Succeeded)
      {
        return true;
      }
    }

    return false;
  }

  // The logged-in employee's ID, read from the JWT "sub" claim.
  // Returns null if the claim is missing or isn't a number.
  private int? GetCurrentEmployeeId()
  {
    var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    return int.TryParse(sub, out var id) ? id : null;
  }
}