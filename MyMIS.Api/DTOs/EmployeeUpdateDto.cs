using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class EmployeeUpdateDto
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

    // Optional on update — null/empty means "don't change the password"
    [MinLength(8)]
    public string? Password { get; set; }

    public int? DepartmentId { get; set; }
}