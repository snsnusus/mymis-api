using System.Text.Json.Serialization;
using MyMIS.Api.Models;

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

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public AvatarStyle? AvatarStyle { get; set; }

  public string Gender { get; set; } = string.Empty;

  public DateOnly? Birthdate { get; set; }

  public string MaritalStatus { get; set; } = string.Empty;

  public string Username { get; set; } = string.Empty;
  public string EmployeeCode { get; set; } = string.Empty;

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public EmployeeType? EmployeeType { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public EmploymentStatus? EmploymentStatus { get; set; }

  public DateOnly? JoiningDate { get; set; }

  public string? OfficeLocation { get; set; }

  public string? WorkSchedule { get; set; }


  public int? DepartmentId { get; set; }

  public string? DepartmentName { get; set; }
  public EmployeePositionDto? Position { get; set; }

  public EmployeePersonalDetailDto? PersonalDetail { get; set; }

  public List<HobbyResponseDto> Hobbies { get; set; } = [];



}