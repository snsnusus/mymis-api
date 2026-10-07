using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class EmployeePhoneResponseDto
{
  public int Id { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public ContactOwnership Ownership { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public PhoneLineType LineType { get; set; }

  public PhoneResponseDto Phone { get; set; } = null!;

  public bool IsPrimary { get; set; }
}