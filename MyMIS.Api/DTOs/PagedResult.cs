namespace MyMIS.Api.DTOs;

// Generic wrapper for any paged list endpoint:
// PagedResult<EmployeeSummaryResponseDto>, later PagedResult<SomethingElseDto>.
public class PagedResult<T>
{
  public List<T> Data { get; set; } = [];

  // Rows across ALL pages (after any search), so the client can compute page count.
  public int TotalCount { get; set; }

  // 1-based page number actually applied (after the controller's clamping).
  public int Page { get; set; }

  public int PageSize { get; set; }
}