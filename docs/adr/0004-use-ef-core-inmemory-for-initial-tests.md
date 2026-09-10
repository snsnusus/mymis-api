# 4. Use EF Core InMemory provider for initial unit tests, Testcontainers later

## Status

Accepted

## Context

`MyMIS.Api` needed its first real unit test coverage, starting with
`EmployeeService` and `DepartmentService`. Both services depend on `AppDbContext`,
which in production is backed by PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`.

Tests need a database backend that:

- Requires no external dependencies (no Docker container, no network call) to run,
  so tests are fast and can run anywhere (local machine, CI runner) with zero setup.
- Provides genuine isolation between test runs, so tests can execute in any order,
  including in parallel, without polluting each other's data.
- Is "real enough" to exercise the actual LINQ queries, `.Include()` calls, and
  business logic inside the services under test.

Two realistic options existed:

1. **EF Core InMemory provider** (`Microsoft.EntityFrameworkCore.InMemory`) — a
   lightweight, in-process fake database provider built for exactly this use case.
2. **Testcontainers** — spins up a real, disposable PostgreSQL container per test
   run, giving genuine relational database behavior.

## Decision

Use the **EF Core InMemory provider** for the initial test suite
(`EmployeeServiceTests`, `DepartmentServiceTests`), with each test getting its own
uniquely-named in-memory database (via `Guid.NewGuid().ToString()` passed to
`UseInMemoryDatabase`), constructed fresh in the test class constructor and
disposed after each test via `IDisposable`.

**Testcontainers is explicitly deferred**, flagged as the next step once tests are
needed that depend on genuine relational/constraint behavior InMemory cannot
provide — specifically:

- Foreign key constraint enforcement
- Unique index enforcement
- `DeleteBehavior.Restrict` (used on `Department.PrimaryContactId`/`SecondaryContactId`)
- Any SQL-translation-specific behavior particular to PostgreSQL

## Consequences

**Positive:**

- Zero external setup required to run the test suite — no Docker, no network,
  works identically on a local machine and in GitHub Actions CI.
- Fast test execution, since there's no container startup cost per test run.
- Sufficient to validate the actual business logic under test today: password
  hashing behavior, null-handling, soft-delete query filter behavior, and
  DTO-mapping correctness (e.g. `PrimaryContactName`/`SecondaryContactName`
  flattening, `EmployeeCount` accuracy).
- Low barrier to entry for learning testing concepts, before introducing the
  added complexity of container-based test infrastructure.

**Negative / known limitations:**

- InMemory does **not** enforce relational constraints. A test suite relying
  solely on InMemory could pass while a genuinely broken constraint (e.g. a
  duplicate `EmployeeCode`, which has no uniqueness constraint configured but
  arguably should) would only surface once running against real PostgreSQL.
  This was observed directly during test-writing, when two test employees
  were accidentally given the same `EmployeeCode` with no test failure result.
- `DeleteBehavior.Restrict` on `Department`'s contact foreign keys is entirely
  untested today — InMemory has no mechanism to verify it, since it doesn't
  enforce delete restrictions at all.
- This creates a known gap: "passes locally/in CI" does not yet fully guarantee
  "passes against real production-equivalent database behavior." This gap is
  accepted as a deliberate, temporary trade-off, not an oversight.

**Follow-up:**

- Introduce Testcontainers (`Testcontainers.PostgreSql` or similar) once tests
  are needed for relational constraint behavior — most likely triggered by
  work on the `Department` contact-reconciliation business rule (see backlog),
  or when the `EmployeeCode`/`Username` uniqueness gap is addressed.
- Existing InMemory-based tests are not expected to be discarded when
  Testcontainers is introduced — the two are expected to coexist, with
  Testcontainers used specifically where relational behavior is the point
  of the test.
