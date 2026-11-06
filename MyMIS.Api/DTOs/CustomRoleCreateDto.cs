using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

/// <summary>
/// A role created for the new employee during employee creation. Its permissions are always
/// granted at <c>Department</c> scope and must come from
/// <see cref="MyMIS.Api.Authorization.PermissionNames.Customizable"/>.
/// </summary>
public class CustomRoleCreateDto
{
  [Required, MaxLength(50)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(200)]
  public string? Description { get; set; }

  [Required, MinLength(1, ErrorMessage = "Choose at least one permission.")]
  public List<string>? Permissions { get; set; }
}