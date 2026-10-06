using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.Dtos;

public class EmployeeAddressResponseDto
{
  public int Id { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public AddressType Type { get; set; }

  public AddressResponseDto Address { get; set; } = null!;

  public bool IsPrimary { get; set; }
}