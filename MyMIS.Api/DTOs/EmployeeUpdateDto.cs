using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyMIS.Api.Models;
using MyMIS.Api.Validation;

namespace MyMIS.Api.DTOs;

public class EmployeeUpdateDto
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

  [Required, ValidUsername]
  public string Username { get; set; } = string.Empty;

  // Optional on update — null/empty means "don't change the password"
  [MinLength(8)]
  public string? Password { get; set; }

  [Required]
  public string MaritalStatus { get; set; } = string.Empty;

  [Required]
  [EnumDataType(typeof(EmploymentStatus))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public EmploymentStatus? EmploymentStatus { get; set; }

  [Required]
  public DateOnly? JoiningDate { get; set; }

  public string? OfficeLocation { get; set; }

  public string? WorkSchedule { get; set; }

  public EmployeePersonalDetailUpdateDto? PersonalDetail { get; set; }
}