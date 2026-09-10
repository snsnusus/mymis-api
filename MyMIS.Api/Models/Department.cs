using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.Models;

public class Department
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    [MaxLength(50)]
    public string? CostCenterCode { get; set; }

    public string? CoverImageUrl { get; set; }

    // Foreign keys to Employee — an employee is the primary/secondary contact
    public int? PrimaryContactId { get; set; }
    public Employee? PrimaryContact { get; set; }

    public int? SecondaryContactId { get; set; }
    public Employee? SecondaryContact { get; set; }

    public string? OfficeId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "active";

    // The other side of the relationship — one Department has many Employees
    public ICollection<Employee> Employees { get; set; } = [];
}