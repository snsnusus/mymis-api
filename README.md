# MyMIS.Api

The .NET Web API backend for MyMIS, an internal HR / Management Information
System. It is the system of record for employees and reference data, and it
issues the JWTs that both [`mymis-portal`](https://github.com/snsnusus/mymis-portal)
and [`mymis-chat-service`](https://github.com/snsnusus/mymis-chat-service) trust.

This project serves a dual purpose: building a real backend, and deepening
C#/.NET backend expertise as part of a structured, hands-on learning plan.

**Live:** <https://mymis-api.azurewebsites.net> (API docs at `/scalar/v1`)

## Tech Stack

| Layer                | Choice                                                            |
| -------------------- | ----------------------------------------------------------------- |
| Language / Framework | C# / .NET 10, ASP.NET Core Web API (Controllers)                  |
| ORM                  | Entity Framework Core (Npgsql)                                    |
| Database             | PostgreSQL 17 (local via Docker Compose, Supabase in prod)        |
| Auth                 | Custom JWT (HS256) + rotating refresh tokens, `BCrypt.Net-Next`   |
| Authorization        | Roles + permission policies + custom handlers                     |
| File storage         | AWS S3 (avatars) + a C# Lambda for thumbnails                     |
| API docs             | Scalar (`Scalar.AspNetCore`) on `Microsoft.AspNetCore.OpenApi`    |
| Testing              | xUnit, EF Core InMemory, `WebApplicationFactory` + Testcontainers |
| CI / Hosting         | GitHub Actions / Azure App Service (Docker image in ACR)          |

## What the API covers

| Area             | Highlights                                                                                                             |
| ---------------- | ---------------------------------------------------------------------------------------------------------------------- |
| Auth             | Login, refresh (rotation + reuse detection), logout, change password                                                   |
| Employees        | Paged/searchable list, lookup, create (one transaction), three PUT tiers, soft delete, server-generated employee codes |
| Employee details | Addresses, phones, emails, emergency contacts, government IDs, hobbies, avatar upload/style                            |
| HMO              | Providers, plans, coverage; employee enrollment with dependents                                                        |
| Organization     | Departments, positions, offices                                                                                        |
| Locations        | Regions, cities, barangays, with bulk upload                                                                           |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker (Docker Desktop or Rancher Desktop), for local PostgreSQL and for the
  integration tests
- The EF Core CLI: `dotnet tool install --global dotnet-ef`

### Setup

1. Clone the repo:

```bash
   git clone https://github.com/snsnusus/mymis-api.git
   cd mymis-api
```

2. Start the local PostgreSQL container (`mymis-postgres`, port 5432):

```bash
   docker compose up -d
```

3. Set your local configuration with user secrets (see
   [Configuration](#configuration) for every key):

```bash
   cd MyMIS.Api
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=MyMIS;Username=postgres;Password=localdevpassword"
   dotnet user-secrets set "Jwt:Key" "<at least 32 random characters>"
   cd ..
```

4. Apply database migrations:

```bash
   dotnet ef database update --project MyMIS.Api
```

5. Run the API over HTTPS:

```bash
   dotnet run --project MyMIS.Api --launch-profile https
```

The first time, trust the development certificate:
`dotnet dev-certs https --trust`. API docs are at
<https://localhost:7129/scalar/v1>.

### Configuration

Base values live in `appsettings.json` / `appsettings.Development.json`.
Secrets are **never committed**: locally they come from `dotnet user-secrets`,
in Azure from App Settings (where `:` becomes `__`, e.g. `Jwt__Key`).

| Key                                                          | Purpose                                                                       |
| ------------------------------------------------------------ | ----------------------------------------------------------------------------- |
| `ConnectionStrings:DefaultConnection`                        | Postgres connection string                                                    |
| `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`                      | Token signing and validation (must match `mymis-chat-service`)                |
| `Jwt:AccessTokenExpiryMinutes`, `Jwt:RefreshTokenExpiryDays` | Token lifetimes                                                               |
| `Aws:AccessKey`, `Aws:SecretKey`                             | IAM user for S3 (`Aws:Region` and `Aws:BucketName` are in `appsettings.json`) |
| `Internal:CallbackSecret`                                    | Shared secret the thumbnail Lambda sends back                                 |
| `Cors:AllowedOrigins`                                        | Browser origins allowed to call the API (empty = none)                        |
| `Features:EnableApiDocs`                                     | Serves Scalar/OpenAPI when `true` (off by default)                            |

### Granting permissions

Endpoints like creating employees need named permissions (e.g.
`employees.create`, `employees.update`, `hmo.manage`, `locations.manage`,
`offices.manage`). The permission rows are seeded by migrations; granting one
to an employee is a direct SQL insert into `EmployeePermissions` for now.
Permissions are baked into the JWT, so the user must log in again.

## Running Tests

```bash
dotnet test                                # everything
dotnet test MyMIS.Api.Tests                # unit tests only (no Docker needed)
dotnet test MyMIS.Api.IntegrationTests     # integration tests (needs Docker)
```

- **`MyMIS.Api.Tests`:** unit tests for services, DTO validation, helpers and
  authorization handlers, using the EF Core InMemory provider.
- **`MyMIS.Api.IntegrationTests`:** the real app in memory via
  `WebApplicationFactory<Program>`, against a throwaway Postgres 17 container
  (Testcontainers) and a fake S3. They cover routing, authorization, model
  validation, unique and foreign-key constraints, and the full employee create
  flow — things InMemory can't prove.

## Project Structure

```
mymis-api/
├── .github/workflows/
│   ├── build.yml               ← CI on pull requests
│   ├── deploy.yml              ← deploy on push to master
│   └── warm-cache.yml          ← keeps the NuGet cache warm
├── docs/adr/                   ← Architecture Decision Records
├── docker-compose.yml          ← local Postgres container + volume
├── MyMIS.Api.slnx
├── MyMIS.Api/                  ← main project
│   ├── Authorization/          ← requirements and handlers (DepartmentScope, SameDepartment, Permission)
│   ├── Controllers/
│   ├── Data/                   ← AppDbContext and model configuration
│   ├── DTOs/                   ← request/response shapes
│   ├── Exceptions/
│   ├── Filters/                ← unique / foreign-key violation → 409/400
│   ├── Helpers/                ← rules and mappings (UniqueConstraints, UsernameRules, ...)
│   ├── Migrations/
│   ├── Models/                 ← EF Core entities
│   ├── OpenApi/                ← document transformers (Bearer auth in Scalar)
│   ├── Options/                ← strongly typed config (JwtOptions, S3Options)
│   ├── Services/               ← business logic
│   ├── Validation/             ← custom validation attributes
│   └── Dockerfile
├── MyMIS.Api.Tests/            ← xUnit unit tests
└── MyMIS.Api.IntegrationTests/ ← WebApplicationFactory + Testcontainers
```

Requests flow **controller → service → `AppDbContext`**. Controllers handle
HTTP concerns (routing, authorization checks, status codes); services hold
the rules and return `ServiceResult<T>` for expected failures.

## Conventions

- **Unique indexes are mapped to 409s.** Every new unique index needs an
  entry in `Helpers/UniqueConstraints.cs`; the global filter turns a database
  violation into a `ValidationProblemDetails` naming the field.
- **Existence before authorization:** return 404 before 403, so a missing
  resource never reveals who may access it.
- **Self-service routes use the JWT `sub` claim** (`/api/Employees/me/...`),
  never an id from the URL, to prevent IDOR.
- **Id routes are constrained** as `{id:int}`.
- **Soft delete** (`DeletedAt` + a global query filter) for employee data;
  reference data uses `IsActive` instead.
- **Conventional Commits** (`feat:`, `fix:`, `refactor:`, `test:` ...).

## CI/CD

- **`build.yml`** runs on every pull request to `master`: restore → build
  (Release) → test, with the NuGet cache keyed on `packages.lock.json`. A PR
  needs it green.
- **`deploy.yml`** runs on every push to `master`:
  1. builds an EF Core migrations bundle (`efbundle`) and applies pending
     migrations to Supabase,
  2. logs in to Azure with **OIDC** (no stored passwords),
  3. builds the Docker image, tags it with the commit SHA and pushes it to
     ACR (`mymisacr`),
  4. points the App Service (`mymis-api`) at the new image and restarts it.
     The repo works on **`master` only**, with short-lived feature branches merged
     through pull requests.

## Known Limitations

- `GET /api/Employees/{id}` doesn't return addresses, phones or emails yet.
- Soft-deleting an employee doesn't free their phone numbers and emails
  (government IDs are freed).
- `MustChangePassword` is reported at login but not enforced server-side.
- There is no endpoint to grant or revoke permissions (SQL only).
- Avatar re-upload with a different file extension leaves the old S3 object.
