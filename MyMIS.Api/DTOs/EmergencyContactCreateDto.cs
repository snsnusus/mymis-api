using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.Dtos;

public class EmergencyContactCreateDto
{
  [Required, MaxLength(100)]
  public string FirstName { get; set; } = string.Empty;

  [MaxLength(100)]
  public string? MiddleName { get; set; }

  [Required, MaxLength(100)]
  public string LastName { get; set; } = string.Empty;

  [MaxLength(10)]
  public string? Suffix { get; set; }

  [Required]
  [EnumDataType(typeof(EmergencyContactRelationship))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public EmergencyContactRelationship? Relationship { get; set; }

  [Required]
  public PhoneDto? Phone { get; set; }

  public AddressDto? Address { get; set; }

  public bool IsPrimary { get; set; }
}