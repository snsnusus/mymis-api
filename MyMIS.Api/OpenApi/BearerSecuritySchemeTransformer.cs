using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MyMIS.Api.OpenApi;

// Adds a "Bearer" security scheme to the generated OpenAPI document,
// so tools like Scalar know the API expects a JWT in the Authorization header.
internal sealed class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
  public async Task TransformAsync(
      OpenApiDocument document,
      OpenApiDocumentTransformerContext context,
      CancellationToken cancellationToken)
  {
    // Only describe Bearer auth if the app actually registered it (AddJwtBearer).
    var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
    if (!schemes.Any(s => s.Name == "Bearer"))
    {
      return;
    }

    // 1. Declare the scheme once, under components.securitySchemes.
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
    {
      ["Bearer"] = new OpenApiSecurityScheme
      {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        In = ParameterLocation.Header,
        BearerFormat = "JWT",
      },
    };

    // 2. Mark every operation as using it, so the token is sent on every request.
    var operations = document.Paths.Values
      .SelectMany(path => path.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>());

    foreach (var operation in operations)
    {
      operation.Security ??= [];
      operation.Security.Add(new OpenApiSecurityRequirement
      {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
      });
    }
  }
}