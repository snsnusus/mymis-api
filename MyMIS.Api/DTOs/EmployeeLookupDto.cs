using System.Text.Json.Serialization;
using MyMIS.Api.Models;

namespace MyMIS.Api.DTOs;

// Deliberately tiny: just enough to render one row in an autocomplete
// dropdown (name, position, small avatar). Not the same type as
// EmployeeSummaryResponseDto, because the list table and the dropdown
// will grow in different directions.
public class EmployeeLookupDto
{
  public int Id { get; set; }

  public string FirstName { get; set; } = string.Empty;

  public string LastName { get; set; } = string.Empty;

  public string? PositionTitle { get; set; }

  // Used by Department Create's "already assigned to a department" warning.
  public int? DepartmentId { get; set; }

  // Thumbnail if one exists, otherwise the original avatar, otherwise null.
  public string? AvatarUrl { get; set; }

  // Style for the generated avatar when AvatarUrl is null.
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public AvatarStyle? AvatarStyle { get; set; }
}