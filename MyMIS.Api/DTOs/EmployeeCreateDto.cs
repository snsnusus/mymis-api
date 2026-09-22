using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class EmployeeCreateDto
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string MiddleName { get; set; } = string.Empty;

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

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty; // plain text ONLY at this boundary — hashed immediately in the service

    public int? DepartmentId { get; set; }

    public int? PositionId { get; set; }

    public string? AvatarUrl { get; set; }

    // NEW — optional. Null means HR skipped this section entirely;
    // CreateAsync still attaches a PersonalDetail row either way,
    // just with all-null fields in that case.
    public EmployeePersonalDetailCreateDto? PersonalDetail { get; set; }

}