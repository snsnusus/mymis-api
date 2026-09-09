# MyMIS-API

A .NET Web API backend for an internal MIS/HR system, built as a companion to
the `sourcefit-clone-portal` frontend. This project serves a dual purpose:
building a real backend, and deepening C#/.NET backend expertise as part of a
structured, hands-on learning plan.

## Tech Stack

| Layer | Choice |
|---|---|
| Language / Framework | C# / .NET 10, ASP.NET Core Web API (Controllers) |
| ORM | Entity Framework Core |
| Database | PostgreSQL 17 (local via Docker Compose) |
| API docs | Scalar (`Scalar.AspNetCore`) |
| Password hashing | `BCrypt.Net-Next` |
| Testing | xUnit + EF Core InMemory provider |
| CI | GitHub Actions |

## Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker (via Docker Desktop or Rancher Desktop) for local PostgreSQL
- `dotnet user-secrets` for local configuration (no secrets are committed to
  this repo)

### Setup

1. Clone the repo:
   ```bash
   git clone https://github.com/snsnusus/sourcefit-clone-api.git
   cd sourcefit-clone-api
   ```

2. Start the local PostgreSQL container:
   ```bash
   docker compose up -d
   ```

3. Restore dependencies:
   ```bash
   dotnet restore
   ```

4. Apply database migrations:
   ```bash
   dotnet ef database update --project SourcefitClone.Api
   ```

5. Run the API:
   ```bash
   dotnet run --project SourcefitClone.Api
   ```

### Running Tests

```bash
dotnet test
```

Tests use the EF Core InMemory provider — no database container required to
run the test suite.

## Project Structure

```
sourcefit-clone-api/
├── .github/workflows/          ← CI and branch promotion automation
├── docs/adr/                   ← Architecture Decision Records
├── docker-compose.yml          ← local Postgres container + volume
├── SourcefitClone.sln
├── SourcefitClone.Api/         ← main project
│   ├── Controllers/
│   ├── Models/
│   ├── DTOs/
│   ├── Services/
│   ├── Data/
│   └── Migrations/
└── SourcefitClone.Api.Tests/   ← xUnit test project
```

## Branching & Promotion Strategy

This repo uses a four-tier environment branch strategy:

```
sit → qa → prerelease → master
```

- **`sit`** — system integration / experimentation.
- **`qa`** — QA testing sandbox.
- **`prerelease`** — pre-production / UAT.
- **`master`** — production (default branch).

All four branches are protected: changes require a pull request and a passing
CI check (`dotnet build` + `dotnet test`).

Promotion between environment branches is automated — merging into `sit`,
`qa`, or `prerelease` automatically opens a pull request promoting that
branch's content into the next stage. Each promotion PR still requires a
manual review and merge; nothing auto-merges.

Feature work should branch off `sit` and be merged back into `sit` via pull
request; it will then be promoted forward automatically.

## Architecture Decisions

Significant technical decisions are documented as ADRs in
[`docs/adr/`](./docs/adr/), including:

- Choice of PostgreSQL over SQL Server
- Soft delete strategy for `Employee` records
- Local Docker Compose setup
- Testing strategy (EF Core InMemory now, Testcontainers later)
- Squash-merge strategy for the branch promotion pipeline

## Contributing

This is currently a solo learning project. See `docs/adr/` for the reasoning
behind key decisions before making significant changes.
