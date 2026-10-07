using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class EmployeeEmailResponseDto
{
  public int Id { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public ContactOwnership Ownership { get; set; }

  public string Email { get; set; } = string.Empty;

  public bool IsPrimary { get; set; }
}