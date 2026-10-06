using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MyMIS.Api.Helpers;

public static class ClaimsPrincipalExtensions
{
  // The logged-in employee's id from the JWT "sub" claim, or null if missing/invalid.
  public static int? GetEmployeeId(this ClaimsPrincipal user)
  {
    var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
    return int.TryParse(sub, out var id) ? id : null;
  }
}