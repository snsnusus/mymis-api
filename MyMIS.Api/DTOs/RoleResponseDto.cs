namespace MyMIS.Api.DTOs;

public class RoleResponseDto : RoleSummaryResponseDto
{
  public List<RolePermissionResponseDto> Permissions { get; set; } = [];

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }
}