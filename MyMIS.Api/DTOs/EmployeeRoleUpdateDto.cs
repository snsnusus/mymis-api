using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class EmployeeRoleUpdateDto
{
  // Nullable + [Required], the same pattern as RoleCreateDto.Permissions:
  // a MISSING roleId stays null and becomes a 400, instead of silently
  // defaulting to 0 (the default for a plain int) and failing later as
  // "role 0 not found".
  [Required]
  public int? RoleId { get; set; }
}