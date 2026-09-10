using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class DepartmentCreateDto
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? PrimaryContactId { get; set; }
    public int? SecondaryContactId { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "active";
}