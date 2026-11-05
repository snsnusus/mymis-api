using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class EmployeeSelfUpdateDto
{
  public string? Nickname { get; set; }

  public string? Birthplace { get; set; }

  public string? Nationality { get; set; }

  [MaxLength(50)]
  public string? BloodType { get; set; }

  [MaxLength(100)]
  public string? Religion { get; set; }

  public string? Bio { get; set; }

  /// <summary>Shows gender to other employees. <c>null</c> keeps the current value.</summary>
  public bool? ShowGenderInProfile { get; set; }

  /// <summary>Shows the birthday (month and day) to other employees. <c>null</c> keeps the current value.</summary>
  public bool? ShowBirthdayInProfile { get; set; }
}