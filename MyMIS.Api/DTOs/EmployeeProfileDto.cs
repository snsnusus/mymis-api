using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

/// <summary>
/// What another employee sees on <c>GET /api/Employees/{id}</c> (design notes, section 7).
/// Fields are picked in explicitly, so nothing added to <see cref="EmployeeResponseDto"/> leaks here.
/// </summary>
public class EmployeeProfileDto
{
  public int Id { get; set; }

  public string FirstName { get; set; } = string.Empty;

  public string MiddleName { get; set; } = string.Empty;

  public string LastName { get; set; } = string.Empty;

  public string? Suffix { get; set; }

  public string? AvatarUrl { get; set; }

  public string? AvatarThumbnailUrl { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public AvatarStyle? AvatarStyle { get; set; }

  public string EmployeeCode { get; set; } = string.Empty;

  public int? DepartmentId { get; set; }

  public string? DepartmentName { get; set; }

  public EmployeePositionDto? Position { get; set; }

  public string? OfficeLocation { get; set; }

  public string? WorkSchedule { get; set; }

  public DateOnly? JoiningDate { get; set; }

  public string? Nickname { get; set; }

  public string? Bio { get; set; }

  public List<HobbyResponseDto> Hobbies { get; set; } = [];

  public List<EmployeePhoneResponseDto> Phones { get; set; } = [];

  public List<EmployeeEmailResponseDto> Emails { get; set; } = [];

  /// <summary>City and region of each address the employee chose to show. Never the street.</summary>
  public List<EmployeeProfileLocationDto> Locations { get; set; } = [];

  /// <summary>Omitted unless the employee opted in.</summary>
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public string? Gender { get; set; }

  /// <summary>Month and day only; omitted unless the employee opted in.</summary>
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public EmployeeBirthdayDto? Birthday { get; set; }
}