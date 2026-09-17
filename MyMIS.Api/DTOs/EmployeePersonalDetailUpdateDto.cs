using System.ComponentModel.DataAnnotations;

namespace MyMIS.Api.DTOs;

public class EmployeePersonalDetailUpdateDto
{
    public string? Nickname { get; set; }

    public string? Birthplace { get; set; }

    public string? Nationality { get; set; }

    [MaxLength(50)]
    public string? BloodType { get; set; }

    [MaxLength(100)]
    public string? Religion { get; set; }

    public string? Bio { get; set; }
}