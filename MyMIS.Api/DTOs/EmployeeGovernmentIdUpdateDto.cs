using System.ComponentModel.DataAnnotations;
using MyMIS.Api.Validation;

namespace MyMIS.Api.DTOs;

public class EmployeeGovernmentIdUpdateDto
{
  [MaxLength(20)]
  [ValidGovernmentId(GovernmentIdType.Sss)]
  public string? SssNumber { get; set; }

  [MaxLength(20)]
  [ValidGovernmentId(GovernmentIdType.PhilHealth)]
  public string? PhilHealthNumber { get; set; }

  [MaxLength(20)]
  [ValidGovernmentId(GovernmentIdType.PagIbig)]
  public string? PagIbigNumber { get; set; }

  [MaxLength(20)]
  [ValidGovernmentId(GovernmentIdType.Tin)]
  public string? TinNumber { get; set; }
}