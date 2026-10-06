using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class EmployeePhoneCreateDto
{
  [Required]
  [EnumDataType(typeof(PhoneOwnership))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public PhoneOwnership? Ownership { get; set; }

  [Required]
  public PhoneDto? Phone { get; set; }

  public bool IsPrimary { get; set; }
}