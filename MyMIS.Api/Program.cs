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
using MyMIS.Api.Options;
using MyMIS.Api.Services;
using Scalar.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

var awsOptions = builder.Configuration.GetSection("Aws").Get<S3Options>()
  ?? throw new InvalidOperationException("Aws configuration section is missing or incomplete.");

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
  ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BarangayService>();
builder.Services.AddScoped<CityService>();
builder.Services.AddScoped<DepartmentService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<HobbyService>();
builder.Services.AddScoped<PositionService>();
builder.Services.AddScoped<RegionService>();
builder.Services.AddScoped<S3UploadService>();
builder.Services.AddScoped<TokenService>();

builder.Services.AddScoped<IAuthorizationHandler, DepartmentScopeHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<IAuthorizationHandler, SameDepartmentHandler>();

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
      RoleClaimType = "role",
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

builder.Services.AddAuthorizationBuilder()
  .AddPolicy("DepartmentScope", policy =>
    policy.Requirements.Add(new DepartmentScopeRequirement()))
  .AddPolicy("employees.create", policy =>
    policy.Requirements.Add(new PermissionRequirement("employees.create")))
  .AddPolicy("employees.update", policy =>
    policy.Requirements.Add(new PermissionRequirement("employees.update")))
  .AddPolicy("positions.create", policy =>
    policy.Requirements.Add(new PermissionRequirement("positions.create")))
  .AddPolicy("positions.update", policy =>
    policy.Requirements.Add(new PermissionRequirement("positions.update")))
  .AddPolicy("SameDepartment", policy =>
    policy.Requirements.Add(new SameDepartmentRequirement()))
  .AddPolicy("locations.manage", policy =>
    policy.Requirements.Add(new PermissionRequirement("locations.manage")));

builder.Services.AddCors(options =>
{
  options.AddPolicy("PortalPolicy", policy =>
  {
    policy.WithOrigins(allowedOrigins)
      .AllowAnyHeader()
      .AllowAnyMethod();
  });
});

builder.Services.AddSingleton<IAmazonS3>(_ =>
{
  var credentials = new BasicAWSCredentials(awsOptions.AccessKey, awsOptions.SecretKey);
  var config = new AmazonS3Config
  {
    RegionEndpoint = RegionEndpoint.GetBySystemName(awsOptions.Region)
  };
  return new AmazonS3Client(credentials, config);
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Features:EnableApiDocs"))
{
  app.MapOpenApi();
  app.MapScalarApiReference();
}

app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseCors("PortalPolicy");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
