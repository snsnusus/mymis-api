using Microsoft.AspNetCore.Mvc;
using MyMIS.Api.Helpers;

namespace MyMIS.Api.Controllers;

// Shared parent for API controllers. Holds helpers that every controller
// needs, so they live in one place instead of being copied into each one.
//
// abstract  → it can't be created on its own, so ASP.NET never treats it as
//             a controller and never routes a request to it.
// ControllerBase → it still inherits Ok(), NotFound(), Conflict(), User, etc.,
//             so subclasses keep everything they had before.
public abstract class ApiControllerBase : ControllerBase
{
  // Translates a service outcome into an HTTP response.
  //
  // The caller decides what success looks like (201, 200 or 204) via onSuccess;
  // this method only decides what each error looks like. All errors use
  // { message } to match AuthController's convention.
  //
  // protected → only subclasses can call it, and ASP.NET doesn't expose
  //             non-public methods as endpoints.
  protected ActionResult ToActionResult<T>(ServiceResult<T> result, Func<T, ActionResult> onSuccess) =>
    result.ErrorType switch
    {
      // No error, so hand the value to the caller's success response.
      null => onSuccess(result.Value!),
      ServiceErrorType.NotFound => NotFound(new { message = result.ErrorMessage }),
      ServiceErrorType.Validation => BadRequest(new { message = result.ErrorMessage }),
      ServiceErrorType.Conflict => Conflict(new { message = result.ErrorMessage }),
      // Fail loudly if someone adds a new error type and forgets to map it here.
      _ => throw new InvalidOperationException($"Unhandled service error type: {result.ErrorType}"),
    };
}