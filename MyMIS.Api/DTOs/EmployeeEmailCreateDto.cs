using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class EmployeeEmailCreateDto
{
  [Required]
  [EnumDataType(typeof(ContactOwnership))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public ContactOwnership? Ownership { get; set; }

  [Required]
  [MaxLength(254)]
  [EmailAddress]
  public string? Email { get; set; }

  public bool IsPrimary { get; set; }
}