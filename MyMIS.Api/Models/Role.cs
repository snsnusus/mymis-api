using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class Role
{
  // true only for SuperAdmin: its permission list is managed by the system
  // and the role can't be edited, deleted, or left without an employee.
  public bool IsSystem { get; set; }

  public bool IsCustom { get; set; }

  public int Id { get; set; }

  [Required]
  [MaxLength(50)]
  public string Name { get; set; } = string.Empty;

  [MaxLength(50)]
  public string NormalizedName { get; set; } = string.Empty;

  [MaxLength(200)]
  public string? Description { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  public ICollection<RolePermission> RolePermissions { get; set; } = [];
}