using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class EmergencyContactResponseDto
{
  public int Id { get; set; }

  public string FirstName { get; set; } = string.Empty;

  public string? MiddleName { get; set; }

  public string LastName { get; set; } = string.Empty;

  public string? Suffix { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public EmergencyContactRelationship Relationship { get; set; }

  public PhoneResponseDto Phone { get; set; } = null!;

  public AddressResponseDto? Address { get; set; }

  public bool IsPrimary { get; set; }
}