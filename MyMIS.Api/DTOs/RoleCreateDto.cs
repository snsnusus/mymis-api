using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class RoleCreateDto
{
  [Required]
  [MaxLength(50)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(200)]
  public string? Description { get; set; }

  // Nullable + [Required]: a MISSING list is a 400, never a silent "no permissions".
  // PUT replaces the whole list, so a forgotten field must not wipe a role.
  // An explicit empty list ("permissions": []) is still allowed.
  [Required]
  public List<RolePermissionCreateDto>? Permissions { get; set; }
}