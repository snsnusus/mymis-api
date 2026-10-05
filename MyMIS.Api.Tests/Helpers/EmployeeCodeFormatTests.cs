using System.Globalization;
using MyMIS.Api.Helpers;

namespace MyMIS.Api.Tests.Helpers;

public class EmployeeCodeFormatTests
{
  [Theory]
  [InlineData(2026, 1L, "MYMIS-2026-00001")]
  [InlineData(2026, 46L, "MYMIS-2026-00046")]
  [InlineData(2027, 99999L, "MYMIS-2027-99999")]
  [InlineData(2027, 100000L, "MYMIS-2027-100000")]
  public void Format_YearAndNumber_ReturnsPaddedCode(int year, long number, string expected)
  {
    // Act
    var result = EmployeeCodeFormat.Format(year, number);

    // Assert
    Assert.Equal(expected, result);
  }

  [Theory]
  [InlineData("2026-06-15T04:00:00Z", 2026)]       // ordinary mid-year moment
  [InlineData("2026-12-31T15:59:59Z", 2026)]       // 23:59:59 Dec 31 in Manila — still 2026
  [InlineData("2026-12-31T16:00:00Z", 2027)]       // 00:00:00 Jan 1 in Manila — already 2027
  [InlineData("2026-12-31T20:00:00-05:00", 2027)]  // same moment rules apply whatever offset the input uses
  public void GetManilaYear_Instant_ReturnsYearInManila(string isoInstant, int expectedYear)
  {
    // Arrange
    var instant = DateTimeOffset.Parse(isoInstant, CultureInfo.InvariantCulture);

    // Act
    var year = EmployeeCodeFormat.GetManilaYear(instant);

    // Assert
    Assert.Equal(expectedYear, year);
  }
}