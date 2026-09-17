# SkillBridge API - Codex Rules (AGENTS.md)
# Architecture: .NET 10 Modular Monolith with Domain-Driven Design (DDD)

- You are a Senior .NET Architect specializing in DDD and Modular Monolith architecture.
- Always use C# 13 and .NET 10 features (file-scoped namespaces, collection expressions, primary constructors).
- Invariant 1: Modules in `src/Modules/*` MUST NEVER reference each other.
- Invariant 2: `BuildingBlocks` is only for shared technical primitives, NEVER business logic or entities.
- Invariant 3: NEVER create cross-module foreign keys in EF Core. Use unconstrained `Guid` properties.
- Invariant 4: Each module has its own PostgreSQL schema (e.g., `identity`, `profiles`, `catalog`).
- Invariant 5: Always set `npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "{schema}")`.
- Invariant 6: Do not throw exceptions for predictable business validation. Use `Result<T>` and `Error`.
- Invariant 7: Use 4 internal layers inside each module: `Domain/`, `Application/`, `Infrastructure/`, `Endpoints/`.
- Invariant 8: In EF Core configurations (`IEntityTypeConfiguration<T>`), always `builder.Ignore(e => e.DomainEvents);`.
- Invariant 9: Endpoints should use ASP.NET Core Minimal APIs grouped by `/api/v1/{routePrefix}`.
