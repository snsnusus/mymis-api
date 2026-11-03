using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class EmployeePhoneCreateDto
{
  [Required]
  [EnumDataType(typeof(ContactOwnership))]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public ContactOwnership? Ownership { get; set; }

  [Required]
  public PhoneDto? Phone { get; set; }

  public bool IsPrimary { get; set; }

  // null (not sent) = on create: visible (default); on update: keep the current value.
  public bool? ShowInProfile { get; set; }
}