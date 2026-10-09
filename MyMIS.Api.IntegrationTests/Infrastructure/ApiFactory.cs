using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using MyMIS.Api.Data;
using MyMIS.Api.DTOs;
using MyMIS.Api.Models;
using Testcontainers.PostgreSql;

namespace MyMIS.Api.IntegrationTests.Infrastructure;

// Starts the real API in memory (WebApplicationFactory) against a real, throwaway
// Postgres container (Testcontainers). One instance is shared by every integration test.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
  public const string CallbackSecret = "integration-test-callback-secret";
  public const string DefaultPassword = "Integration-Pass-2026!";

  private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17").Build();

  // Replaces the real S3 client, so no test ever talks to AWS.
  public Mock<IAmazonS3> S3 { get; } = new();

  public ApiFactory()
  {
    S3.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new PutObjectResponse());

    S3.Setup(c => c.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
      .Returns((GetPreSignedUrlRequest request) => $"https://fake-presigned-url.test/{request.Key}");
  }

  // Runs before any test: start Postgres, point the app at it, apply the real migrations.
  public async Task InitializeAsync()
  {
    await _database.StartAsync();
    var connectionString = _database.GetConnectionString();

    // Program.cs reads the Jwt and Aws sections the moment it starts, before any test hook
    // could add settings, so they are supplied as environment variables. Environment variables
    // also override everything else, including user secrets.
    Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);
    Environment.SetEnvironmentVariable("Jwt__Key", "integration-tests-signing-key-at-least-32-chars!");
    Environment.SetEnvironmentVariable("Jwt__Issuer", "mymis-integration-tests");
    Environment.SetEnvironmentVariable("Jwt__Audience", "mymis-integration-tests");
    Environment.SetEnvironmentVariable("Aws__AccessKey", "test-access-key");
    Environment.SetEnvironmentVariable("Aws__SecretKey", "test-secret-key");
    Environment.SetEnvironmentVariable("Internal__CallbackSecret", CallbackSecret);

    // The same migrations production gets, applied to an empty database.
    var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
    await using var context = new AppDbContext(options);
    await context.Database.MigrateAsync();
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    // "Testing" (not Development) means user secrets are never loaded,
    // so these tests can't reach your real database by accident.
    builder.UseEnvironment("Testing");

    builder.ConfigureTestServices(services =>
    {
      services.RemoveAll<IAmazonS3>();
      services.AddSingleton(S3.Object);
    });
  }

  // WebApplicationFactory already has a ValueTask-returning DisposeAsync, so xUnit's Task-returning
  // one is implemented explicitly. The factory still shuts the app down through IDisposable.
  Task IAsyncLifetime.DisposeAsync() => _database.DisposeAsync().AsTask();

  // ---------------- helpers for tests ----------------

  public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
  {
    BaseAddress = new Uri("https://localhost"),
    AllowAutoRedirect = false,
  });

  // Inserts a new employee straight into the database, with a unique username each time,
  // and grants the named permissions (the migrations seed the permission rows).
  public async Task<Employee> SeedEmployeeAsync(Role role = Role.User, params string[] permissions)
  {
    var suffix = Guid.NewGuid().ToString("N")[..10];

    using var scope = Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var employee = new Employee
    {
      FirstName = "Test",
      LastName = suffix,
      Gender = "MALE",
      MaritalStatus = "SINGLE",
      EmployeeCode = $"IT-{suffix}",
      Username = $"it{suffix}",
      PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
      Role = role,
    };

    if (permissions.Length > 0)
    {
      var found = await db.Permissions.Where(p => permissions.Contains(p.Name)).ToListAsync();
      var missing = permissions.Except(found.Select(p => p.Name)).ToList();

      if (missing.Count > 0)
      {
        throw new InvalidOperationException(
          $"These permissions are not seeded by the migrations: {string.Join(", ", missing)}");
      }

      foreach (var permission in found)
      {
        employee.EmployeePermissions.Add(new EmployeePermission { PermissionId = permission.Id });
      }
    }

    db.Employees.Add(employee);
    await db.SaveChangesAsync();
    return employee;
  }

  // Logs in through the real endpoint and returns the access token.
  public async Task<string> LoginAsync(string username, string password = DefaultPassword)
  {
    using var client = CreateApiClient();
    var response = await client.PostAsJsonAsync("/api/Auth/login", new { username, password });
    response.EnsureSuccessStatusCode();

    var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
    return auth!.AccessToken;
  }

  // A client that is already logged in as a freshly seeded employee.
  public async Task<HttpClient> CreateAuthorizedClientAsync(Role role = Role.User, params string[] permissions)
  {
    var employee = await SeedEmployeeAsync(role, permissions);
    var token = await LoginAsync(employee.Username);

    var client = CreateApiClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return client;
  }

  private readonly ConcurrentDictionary<string, Task<HttpClient>> _clients = new();

  // Logging in costs a deliberately slow BCrypt hash plus a BCrypt check, so tests that send many
  // requests as the same kind of user share one logged-in client instead of creating a new one each time.
  // (Access tokens last 15 minutes, which is longer than a test run.)
  public Task<HttpClient> GetClientAsync(Role role = Role.User, params string[] permissions)
  {
    var key = $"{role}|{string.Join(",", permissions.Order())}";
    return _clients.GetOrAdd(key, _ => CreateAuthorizedClientAsync(role, permissions));
  }

  // Runs code against the database in a fresh scope, which means a fresh AppDbContext.
  // A context that just failed a SaveChanges is in a bad state, so every attempt gets a new one.
  public async Task ExecuteDbAsync(Func<AppDbContext, Task> action)
  {
    using var scope = Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await action(db);
  }

  public async Task<T> QueryDbAsync<T>(Func<AppDbContext, Task<T>> query)
  {
    using var scope = Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    return await query(db);
  }
}