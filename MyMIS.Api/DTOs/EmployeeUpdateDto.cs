using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class EmployeeUpdateDto
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Suffix { get; set; }

    [Required]
    public string Gender { get; set; } = string.Empty;

    public DateOnly? Birthdate { get; set; }

    [Required]
    public string MaritalStatus { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string EmployeeCode { get; set; } = string.Empty;

    public string? OfficeLocation { get; set; }

    public string? WorkSchedule { get; set; }

    [Required]
    public string Username { get; set; } = string.Empty;

    // Optional on update — null/empty means "don't change the password"
    [MinLength(8)]
    public string? Password { get; set; }

    public string? AvatarUrl { get; set; }

    public EmployeePersonalDetailUpdateDto? PersonalDetail { get; set; }
}