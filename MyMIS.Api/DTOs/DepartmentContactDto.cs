namespace MyMIS.Api.DTOs;

// A department's primary/secondary contact, nested inside DepartmentResponseDto.
public class DepartmentContactDto
{
  public int Id { get; set; }
  public string FullName { get; set; } = string.Empty;
  public string? PositionTitle { get; set; }
}