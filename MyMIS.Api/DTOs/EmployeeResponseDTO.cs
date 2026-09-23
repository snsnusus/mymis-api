namespace MyMIS.Api.DTOs;

public class EmployeeResponseDto
{
  public int Id { get; set; }

  public string FirstName { get; set; } = string.Empty;

  public string MiddleName { get; set; } = string.Empty;

  public string LastName { get; set; } = string.Empty;

  public string? Suffix { get; set; }

  public string? AvatarUrl { get; set; }

  public string? AvatarThumbnailUrl { get; set; }

  public string Gender { get; set; } = string.Empty;

  public DateOnly? Birthdate { get; set; }

  public string MaritalStatus { get; set; } = string.Empty;

  public string? OfficeLocation { get; set; }

  public string? WorkSchedule { get; set; }

  public string EmployeeCode { get; set; } = string.Empty;

  public string Username { get; set; } = string.Empty;

  public int? DepartmentId { get; set; }

  public string? DepartmentName { get; set; }

  public EmployeePersonalDetailDto? PersonalDetail { get; set; }

  public EmployeePositionDto? Position { get; set; }

  public List<HobbyResponseDto> Hobbies { get; set; } = [];
}