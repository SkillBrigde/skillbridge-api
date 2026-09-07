# ADR 0001: Modular Monolith with module-owned boundaries

- Status: Accepted
- Date: 2026-09-04

## Decision

The backend is deployed as one .NET process. Business capabilities are separate projects and must not reference one another directly. The API project is the composition root. Each module will own a PostgreSQL schema and its migrations.

## Consequences

This keeps deployment simple for a four-person team while preserving boundaries that can later be extracted. A change spanning modules cannot rely on a distributed ACID transaction; integration events and idempotent consumers are required.
