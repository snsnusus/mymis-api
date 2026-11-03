using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyMIS.Api.Authorization;
using MyMIS.Api.Data;
using MyMIS.Api.Filters;
using MyMIS.Api.Options;
using MyMIS.Api.Services;
using MyMIS.Api.OpenApi;
using Scalar.AspNetCore;
using System.Text;
using MyMIS.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

var awsOptions = builder.Configuration.GetSection("Aws").Get<S3Options>()
  ?? throw new InvalidOperationException("Aws configuration section is missing or incomplete.");

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
  ?? throw new InvalidOperationException("Jwt configuration section is missing.");

// In-process key/value cache with expiry (a Map with TTLs). Used by RolePermissionCache.
builder.Services.AddMemoryCache();

builder.Services.AddControllers(options =>
{
  // Turns unique-index violations from any controller action into a 409 (see Helpers/UniqueConstraints.cs).
  options.Filters.Add<UniqueViolationExceptionFilter>();
  options.Filters.Add<ForeignKeyViolationExceptionFilter>();
});

builder.Services.AddOpenApi(options =>
{
  options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BarangayService>();
builder.Services.AddScoped<CityService>();
builder.Services.AddScoped<DepartmentService>();
builder.Services.AddScoped<EmergencyContactService>();
builder.Services.AddScoped<EmployeeAccessGuard>();
builder.Services.AddScoped<EmployeeAddressService>();
builder.Services.AddScoped<IEmployeeCodeGenerator, EmployeeCodeGenerator>();
builder.Services.AddScoped<EmployeeEmailService>();
builder.Services.AddScoped<EmployeeGovernmentIdService>();
builder.Services.AddScoped<EmployeeHmoService>();
builder.Services.AddScoped<EmployeePhoneService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<HmoPlanService>();
builder.Services.AddScoped<HmoProviderService>();
builder.Services.AddScoped<HobbyService>();
builder.Services.AddScoped<OfficeService>();
builder.Services.AddScoped<PositionService>();
builder.Services.AddScoped<RegionService>();
builder.Services.AddScoped<RoleService>();
builder.Services.AddScoped<S3UploadService>();
builder.Services.AddScoped<TokenService>();

builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

builder.Services.AddSingleton<IAmazonS3>(_ =>
{
  var credentials = new BasicAWSCredentials(awsOptions.AccessKey, awsOptions.SecretKey);
  var config = new AmazonS3Config
  {
    RegionEndpoint = RegionEndpoint.GetBySystemName(awsOptions.Region)
  };
  return new AmazonS3Client(credentials, config);
});

// Singleton: one shared cache for the whole app. PermissionHandler (also a
// singleton) receives it through its constructor.
builder.Services.AddSingleton<IRolePermissionCache, RolePermissionCache>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
  options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
  options.KnownIPNetworks.Clear();
  options.KnownProxies.Clear();
});
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<S3Options>(builder.Configuration.GetSection("Aws"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(options =>
  {
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
      NameClaimType = "name",

      ValidateIssuer = true,
      ValidIssuer = jwtOptions.Issuer,

      ValidateAudience = true,
      ValidAudience = jwtOptions.Audience,

      ValidateLifetime = true,
      ClockSkew = TimeSpan.Zero,

      ValidateIssuerSigningKey = true,
      IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.Key))
    };
  });

// Keep the builder in a variable so we can keep adding policies after the chain.
var authorization = builder.Services.AddAuthorizationBuilder();

// One policy per catalog permission, named exactly like the permission.
foreach (var permission in PermissionNames.All)
{
  authorization.AddPolicy(permission, policy =>
    policy.Requirements.Add(new PermissionRequirement(permission)));
}

builder.Services.AddCors(options =>
{
  options.AddPolicy("PortalPolicy", policy =>
  {
    policy.WithOrigins(allowedOrigins)
      .AllowAnyHeader()
      .AllowAnyMethod();
  });
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Features:EnableApiDocs"))
{
  app.MapOpenApi();
  app.MapScalarApiReference(options => options
    .AddPreferredSecuritySchemes("Bearer")
    .EnablePersistentAuthentication());
}

app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseCors("PortalPolicy");

app.UseAuthentication();

app.UseMiddleware<MustChangePasswordMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Lets the integration-test project start the app with WebApplicationFactory<Program>.
public partial class Program { }
