using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class EmployeeCreateDto
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public string Gender { get; set; } = string.Empty;

    [Required]
    public string MaritalStatus { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string EmployeeCode { get; set; } = string.Empty;

    public string? OfficeLocation { get; set; }

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty; // plain text ONLY at this boundary — hashed immediately in the service

    public int? DepartmentId { get; set; }
}