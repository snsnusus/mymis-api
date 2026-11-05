namespace MyMIS.Api.DTOs;

public class PermissionResponseDto
{
  public string Name { get; set; } = string.Empty;

  public bool CompanyOnly { get; set; }

  public bool Customizable { get; set; }
}