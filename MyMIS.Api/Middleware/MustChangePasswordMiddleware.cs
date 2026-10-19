using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.Authorization;

namespace MyMIS.Api.Middleware;

// Blocks every protected endpoint for a user whose token says they must change
// their password, except endpoints marked [AllowWhenPasswordChangeRequired].
public class MustChangePasswordMiddleware(RequestDelegate next)
{
  private readonly RequestDelegate _next = next;

  public async Task InvokeAsync(HttpContext context)
  {
    if (!ShouldBlock(context))
    {
      await _next(context);
      return;
    }

    var problem = new ProblemDetails
    {
      Status = StatusCodes.Status403Forbidden,
      Title = "Password change required.",
      Detail = "You must change your password before using the rest of the application.",
    };
    problem.Extensions["code"] = "password_change_required";

    context.Response.StatusCode = StatusCodes.Status403Forbidden;
    await context.Response.WriteAsJsonAsync(
      problem,
      options: (System.Text.Json.JsonSerializerOptions?)null,
      contentType: "application/problem+json");
  }

  private static bool ShouldBlock(HttpContext context)
  {
    var claimValue = context.User.FindFirst("mustChangePassword")?.Value;
    if (!bool.TryParse(claimValue, out var mustChangePassword) || !mustChangePassword)
    {
      return false;
    }

    var endpoint = context.GetEndpoint();
    if (endpoint is null)
    {
      return false;
    }

    var isProtected = endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null
      && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;
    if (!isProtected)
    {
      return false;
    }

    var isAllowListed = endpoint.Metadata
      .GetMetadata<AllowWhenPasswordChangeRequiredAttribute>() is not null;

    return !isAllowListed;
  }
}