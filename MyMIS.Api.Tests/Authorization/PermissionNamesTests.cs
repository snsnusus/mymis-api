using System.Linq;
using MyMIS.Api.Authorization;
using Xunit;

namespace MyMIS.Api.Tests.Authorization;

public class PermissionNamesTests
{
  [Fact]
  public void Customizable_ContainsOnlyCatalogPermissions()
  {
    Assert.Subset(
      new HashSet<string>(PermissionNames.All),
      new HashSet<string>(PermissionNames.Customizable));
  }

  [Fact]
  public void Customizable_HasNoCompanyOnlyPermissions()
  {
    Assert.Empty(PermissionNames.Customizable.Intersect(PermissionNames.CompanyOnly));
  }

  [Fact]
  public void Customizable_HasNoDeletePermissions()
  {
    Assert.DoesNotContain(PermissionNames.Customizable, p => p.EndsWith(".delete"));
  }

  /// <summary>
  /// Reference data has no department, so a <c>Department</c>-scoped write on it
  /// would act company-wide. Only reads of it may be customizable.
  /// </summary>
  [Theory]
  [InlineData("offices.")]
  [InlineData("locations.")]
  [InlineData("hmo-providers.")]
  [InlineData("hmo-plans.")]
  public void Customizable_ReferenceDataIsReadOnly(string resourcePrefix)
  {
    var granted = PermissionNames.Customizable.Where(p => p.StartsWith(resourcePrefix));

    Assert.All(granted, p => Assert.EndsWith(".read", p));
  }
}