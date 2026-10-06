using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

// Used for POST and PUT, following EmergencyContactCreateDto.
public class EmployeeAddressCreateDto
{
  [Required]
  [EnumDataType(typeof(AddressType))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public AddressType? Type { get; set; }

  [Required]
  public AddressDto? Address { get; set; }

  public bool IsPrimary { get; set; }
}