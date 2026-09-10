# 3. Use Docker Compose for local PostgreSQL

## Status

Accepted

## Context

The developer did not have Docker Desktop installed, but had Rancher
Desktop already set up (and wanted to build containerization skills
as part of this project). Rancher Desktop is Docker-API-compatible,
so standard `docker`/`docker compose` commands work against it
without modification.

A single `docker run` command could start a Postgres container, but
the project is expected to grow to include additional local
dependencies over time (e.g. Redis, already used experimentally in
an unrelated project). Managing multiple services via separate
manual `docker run` commands does not scale well and is not easily
reproducible by someone else setting up the project fresh.

## Decision

Define local development dependencies in a `docker-compose.yml` file
at the repo root, starting with a single `postgres:17` service. A
named volume (`mymis-pgdata`) is used to persist database
data independently of the container's own lifecycle.

## Consequences

- Running `docker compose up -d` from the repo root starts all local
  dependencies with one command; `docker compose down` stops them.
- Postgres must be running before `dotnet run` will succeed, since
  `AppDbContext` connects to it at startup.
- The named volume must be explicitly removed (`docker compose down
-v`) to fully reset the database — recreating the container alone
  does not clear existing data.
- Additional local dependencies (e.g. Redis) should be added as new
  services in this same file going forward, rather than started
  independently, to keep the whole local stack reproducible from one
  file.
