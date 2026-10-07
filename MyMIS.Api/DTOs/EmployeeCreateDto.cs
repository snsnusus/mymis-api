using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyMIS.Api.Models;
using MyMIS.Api.Validation;

namespace MyMIS.Api.DTOs;

public class EmployeeCreateDto
{
  [Required, MaxLength(100)]
  public string FirstName { get; set; } = string.Empty;

  [Required, MaxLength(100)]
  public string MiddleName { get; set; } = string.Empty;

  [Required, MaxLength(100)]
  public string LastName { get; set; } = string.Empty;

  [MaxLength(20)]
  public string? Suffix { get; set; }

  [Required]
  public string Gender { get; set; } = string.Empty;

  public DateOnly? Birthdate { get; set; }

  [Required]
  public string MaritalStatus { get; set; } = string.Empty;

  [EnumDataType(typeof(AvatarStyle))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public AvatarStyle? AvatarStyle { get; set; }

  [Required]
  [EnumDataType(typeof(EmployeeType))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public EmployeeType? EmployeeType { get; set; }

  [Required]
  [EnumDataType(typeof(EmploymentStatus))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public EmploymentStatus? EmploymentStatus { get; set; }

  [Required]
  public DateOnly? JoiningDate { get; set; }

  public string? OfficeLocation { get; set; }

  public string? WorkSchedule { get; set; }

  [Required, ValidUsername]
  public string Username { get; set; } = string.Empty;

  [Required, MinLength(8)]
  public string Password { get; set; } = string.Empty; // plain text ONLY at this boundary — hashed immediately in the service

  public int? DepartmentId { get; set; }

  public int? PositionId { get; set; }

  // Optional. Null means HR skipped this section entirely;
  // CreateAsync still attaches a PersonalDetail row either way,
  // just with all-null fields in that case.
  public EmployeePersonalDetailCreateDto? PersonalDetail { get; set; }

  [Required]
  public EmergencyContactCreateDto? EmergencyContact { get; set; }

  [Required]
  [MinLength(1, ErrorMessage = "At least one address is required.")]
  public List<EmployeeAddressCreateDto>? Addresses { get; set; }

  [Required]
  [MinLength(1, ErrorMessage = "At least one phone is required.")]
  public List<EmployeePhoneCreateDto>? Phones { get; set; }
}