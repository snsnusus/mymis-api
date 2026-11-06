using System.ComponentModel.DataAnnotations;
using MyMIS.Api.Authorization;
using MyMIS.Api.DTOs;

namespace MyMIS.Api.Tests.DTOs;

public class CustomRoleCreateDtoTests
{
  private static CustomRoleCreateDto ValidDto() => new()
  {
    Name = "Team Lead - Finance",
    Permissions = [PermissionNames.Employees.Read],
  };

  private static List<ValidationResult> Validate(CustomRoleCreateDto dto)
  {
    var results = new List<ValidationResult>();
    Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
    return results;
  }

  [Fact]
  public void Validate_ValidDto_HasNoErrors()
  {
    Assert.Empty(Validate(ValidDto()));
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void Validate_BlankName_ReportsNameError(string name)
  {
    var dto = ValidDto();
    dto.Name = name;

    var error = Assert.Single(Validate(dto));
    Assert.Contains(nameof(CustomRoleCreateDto.Name), error.MemberNames);
  }

  [Fact]
  public void Validate_NameLongerThan50Characters_ReportsNameError()
  {
    var dto = ValidDto();
    dto.Name = new string('a', 51);

    var error = Assert.Single(Validate(dto));
    Assert.Contains(nameof(CustomRoleCreateDto.Name), error.MemberNames);
  }

  [Fact]
  public void Validate_DescriptionLongerThan200Characters_ReportsDescriptionError()
  {
    var dto = ValidDto();
    dto.Description = new string('a', 201);

    var error = Assert.Single(Validate(dto));
    Assert.Contains(nameof(CustomRoleCreateDto.Description), error.MemberNames);
  }

  [Fact]
  public void Validate_MissingPermissions_ReportsPermissionsError()
  {
    var dto = ValidDto();
    dto.Permissions = null;

    var error = Assert.Single(Validate(dto));
    Assert.Contains(nameof(CustomRoleCreateDto.Permissions), error.MemberNames);
  }

  [Fact]
  public void Validate_EmptyPermissions_ReportsPermissionsError()
  {
    var dto = ValidDto();
    dto.Permissions = [];

    var error = Assert.Single(Validate(dto));
    Assert.Contains(nameof(CustomRoleCreateDto.Permissions), error.MemberNames);
  }
}