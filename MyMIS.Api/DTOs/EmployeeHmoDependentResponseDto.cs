using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

public class EmployeeHmoDependentResponseDto
{
  public int Id { get; set; }

  public string FirstName { get; set; } = string.Empty;

  public string? MiddleName { get; set; }

  public string LastName { get; set; } = string.Empty;

  public string? Suffix { get; set; }

  public DateOnly Birthdate { get; set; }

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public HmoDependentRelationship Relationship { get; set; }

  public HmoRefDto Plan { get; set; } = null!;

  public string? MemberNumber { get; set; }
}