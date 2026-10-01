using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class EmergencyContact
{
  public int Id { get; set; }

  public int EmployeeId { get; set; }

  [Required, MaxLength(100)]
  public string FirstName { get; set; } = string.Empty;

  [MaxLength(100)]
  public string? MiddleName { get; set; }

  [Required, MaxLength(100)]
  public string LastName { get; set; } = string.Empty;

  [MaxLength(10)]
  public string? Suffix { get; set; }

  public EmergencyContactRelationship Relationship { get; set; }

  public Phone Phone { get; set; } = null!;

  public Address? Address { get; set; }

  public bool IsPrimary { get; set; }

  public DateTime CreatedAt { get; set; }

  public DateTime UpdatedAt { get; set; }

  // Soft delete: null = active, a value = deleted at that moment.
  public DateTime? DeletedAt { get; set; }
}