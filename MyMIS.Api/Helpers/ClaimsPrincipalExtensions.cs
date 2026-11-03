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

  // The caller's role id from the JWT "roleId" claim (Step 4).
  // null if the claim is missing or not a number (e.g. a token issued before Step 4).
  public static int? GetRoleId(this ClaimsPrincipal user) =>
    int.TryParse(user.FindFirst("roleId")?.Value, out var id) ? id : null;
}