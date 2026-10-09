using System.Text.Json;
using MyMIS.Api.DTOs;
using MyMIS.Api.Helpers;

namespace MyMIS.Api.Tests.Helpers;

public class BulkRowParserTests
{
  // JsonElement stays valid only while its JsonDocument is alive, so clone it out.
  private static JsonElement Json(string json)
  {
    using var document = JsonDocument.Parse(json);
    return document.RootElement.Clone();
  }

  // ---------------- GetRowCountError ----------------

  [Theory]
  [InlineData(0, "The payload must contain at least one row.")]
  [InlineData(1001, "A single upload is limited to 1000 rows.")]
  public void GetRowCountError_OutOfRange_ReturnsTheMessage(int rowCount, string expected)
  {
    Assert.Equal(expected, BulkRowParser.GetRowCountError(rowCount));
  }

  [Theory]
  [InlineData(1)]
  [InlineData(500)]
  [InlineData(1000)]
  public void GetRowCountError_WithinTheLimits_ReturnsNull(int rowCount)
  {
    Assert.Null(BulkRowParser.GetRowCountError(rowCount));
  }

  // ---------------- TryParse: valid rows ----------------

  [Fact]
  public void TryParse_ValidRow_ReturnsTheItemAndNoError()
  {
    // Arrange
    var raw = Json("""{"name":"Bagong Pag-asa","psgcCode":"137404001","zipCode":"1105"}""");

    // Act
    var ok = BulkRowParser.TryParse<BarangayBulkItemDto>(raw, 1, out var item, out var error);

    // Assert
    Assert.True(ok);
    Assert.Null(error);
    Assert.NotNull(item);
    Assert.Equal("Bagong Pag-asa", item.Name);
    Assert.Equal("137404001", item.PsgcCode);
    Assert.Equal("1105", item.ZipCode);
  }

  [Fact]
  public void TryParse_UnknownProperty_IsIgnored()
  {
    // Arrange: a parent id inside a row is ignored, because the parent comes from the query string
    var raw = Json("""{"name":"Bagong Pag-asa","cityId":999}""");

    // Act
    var ok = BulkRowParser.TryParse<BarangayBulkItemDto>(raw, 1, out var item, out _);

    // Assert
    Assert.True(ok);
    Assert.NotNull(item);
    Assert.Equal("Bagong Pag-asa", item.Name);
  }

  [Fact]
  public void TryParse_PropertyNamesAreCaseInsensitive()
  {
    // Arrange
    var raw = Json("""{"NAME":"Bagong Pag-asa"}""");

    // Act
    var ok = BulkRowParser.TryParse<BarangayBulkItemDto>(raw, 1, out var item, out _);

    // Assert
    Assert.True(ok);
    Assert.NotNull(item);
    Assert.Equal("Bagong Pag-asa", item.Name);
  }

  // ---------------- TryParse: rejected rows ----------------

  [Fact]
  public void TryParse_RowIsNotAnObject_ReturnsRowMustBeAnObject()
  {
    // Arrange: e.g. a stray number in the array
    var raw = Json("42");

    // Act
    var ok = BulkRowParser.TryParse<BarangayBulkItemDto>(raw, 3, out var item, out var error);

    // Assert
    Assert.False(ok);
    Assert.Null(item);
    Assert.NotNull(error);
    Assert.Equal(3, error.Row);
    Assert.Equal("Row must be a JSON object.", Assert.Single(error.Errors));
    Assert.Equal(42, error.Data.GetInt32());
  }

  [Fact]
  public void TryParse_ValueOfTheWrongType_ReturnsAnInvalidValueError()
  {
    // Arrange: a number where a string is expected
    var raw = Json("""{"name":123}""");

    // Act
    var ok = BulkRowParser.TryParse<BarangayBulkItemDto>(raw, 2, out _, out var error);

    // Assert
    Assert.False(ok);
    Assert.NotNull(error);
    Assert.Equal(2, error.Row);
    Assert.StartsWith("Invalid value at", Assert.Single(error.Errors));
  }

  [Fact]
  public void TryParse_MissingName_ReturnsARequiredError()
  {
    // Arrange
    var raw = Json("""{"psgcCode":"137404001"}""");

    // Act
    var ok = BulkRowParser.TryParse<BarangayBulkItemDto>(raw, 5, out _, out var error);

    // Assert: the original row is echoed back so the user can find it
    Assert.False(ok);
    Assert.NotNull(error);
    Assert.Equal(5, error.Row);
    Assert.Contains("Name", Assert.Single(error.Errors));
    Assert.Equal("137404001", error.Data.GetProperty("psgcCode").GetString());
  }

  [Fact]
  public void TryParse_WhitespaceOnlyName_ReturnsARequiredError()
  {
    // Arrange
    var raw = Json("""{"name":"   "}""");

    // Act
    var ok = BulkRowParser.TryParse<BarangayBulkItemDto>(raw, 1, out _, out var error);

    // Assert
    Assert.False(ok);
    Assert.NotNull(error);
    Assert.Contains("Name", Assert.Single(error.Errors));
  }

  [Fact]
  public void TryParse_NameLongerThanTheLimit_ReturnsAMaxLengthError()
  {
    // Arrange: 151 characters, the limit is 150
    var raw = Json($$"""{"name":"{{new string('a', 151)}}"}""");

    // Act
    var ok = BulkRowParser.TryParse<BarangayBulkItemDto>(raw, 1, out _, out var error);

    // Assert
    Assert.False(ok);
    Assert.NotNull(error);
    Assert.Contains("Name", Assert.Single(error.Errors));
  }

  [Fact]
  public void TryParse_ZipCodeLongerThanTheLimit_ReturnsAMaxLengthError()
  {
    // Arrange: 11 characters, the limit is 10 (proves validateAllProperties is on)
    var raw = Json("""{"name":"Bagong Pag-asa","zipCode":"12345678901"}""");

    // Act
    var ok = BulkRowParser.TryParse<BarangayBulkItemDto>(raw, 1, out _, out var error);

    // Assert
    Assert.False(ok);
    Assert.NotNull(error);
    Assert.Contains("ZipCode", Assert.Single(error.Errors));
  }

  [Fact]
  public void TryParse_RowWithSeveralProblems_ReportsAllOfThem()
  {
    // Arrange: missing name AND a zip code that is too long
    var raw = Json("""{"zipCode":"12345678901"}""");

    // Act
    var ok = BulkRowParser.TryParse<BarangayBulkItemDto>(raw, 1, out _, out var error);

    // Assert
    Assert.False(ok);
    Assert.NotNull(error);
    Assert.Equal(2, error.Errors.Count);
  }
}