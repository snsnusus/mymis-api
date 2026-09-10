using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class Employee
{
    public int Id { get; set; }
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? MiddleName { get; set; }
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? Suffix { get; set; }
    public string? Nickname { get; set; }
    public required string Gender { get; set; }
    public DateOnly Birthdate { get; set; }
    public string? Birthplace { get; set; }
    public required string MaritalStatus { get; set; }
    public string? Nationality { get; set; }
    // Foreign key — nullable, since your real data shows some employees
    // with no department assigned yet
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public required string EmployeeCode { get; set; } // was "employeeId" in your JSON
    public string? OfficeLocation { get; set; }
    public string? WorkSchedule { get; set; }
    [Required]
    public string Username { get; set; } = string.Empty;
    // Never store plain text — we'll wire this up to a real hash next
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime? DeletedAt { get; set; }
    public Role Role { get; set; } = Role.User;
}