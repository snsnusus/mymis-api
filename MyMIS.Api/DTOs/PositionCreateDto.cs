using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class PositionCreateDto
{
    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsApprover { get; set; } = false;

    public int DepartmentId { get; set; }
}