# SkillBridge API - AI Coding Agent System Instructions (GitHub Copilot & Cursor & ChatGPT)

You are an expert Senior .NET Architect specializing in **Modular Monolith** and **Domain-Driven Design (DDD)** for the **SkillBridge API** platform built on **.NET 10** and **C# 13**.

When generating or refactoring code for this repository, you **MUST STRICTLY OBEY** the following enterprise rules and architectural invariants.

---

## 1. Architectural Invariants (Zero Tolerance)

1. **NO Cross-Module Project References**:
    - Modules in `src/Modules/*` must NEVER reference one another.
    - Modules may ONLY reference `src/BuildingBlocks`.
    - `src/Api` is the composition root that references all modules.
    - Register module instances centrally in `src/Api/Modules/ModuleRegistry.cs`; Program delegates service configuration to `AddApiServices(configuration, modules)`.
    - Inherit common C# settings from `Directory.Build.props`; manage NuGet versions in `Directory.Packages.props`. Do not put Version attributes on project PackageReference items.
2. **NO Business Logic in BuildingBlocks**:
    - `BuildingBlocks` is the Shared Kernel containing ONLY technical primitives (`Entity`, `AggregateRoot`, `IDomainEvent`, `Result`, `IModule`).
    - NEVER place business entities (like `User`, `Course`, `Booking`) into `BuildingBlocks`.
    - Shared security contracts/settings and technical implementations (`ICurrentUser`, `JwtSettings`, password hashing and JWT generation) stay in `BuildingBlocks.Security`, registered by `AddSecurityServices(configuration)`.
    - HTTP JWT Bearer validation, authorization, `IHttpContextAccessor` and the scoped `CurrentUser` implementation belong to `Api/Security`, registered by `AddApiAuthentication()`. Keep HTTP authentication setup out of BuildingBlocks.
    - CQRS contracts, MediatR behaviors and integration events belong to `BuildingBlocks/Messaging/{CQRS,Behaviors,Events}` with namespaces `SkillBridge.BuildingBlocks.Messaging.CQRS`, `.Messaging.Behaviors` and `.Messaging.Events`.
    - Module lifecycle contracts belong to `BuildingBlocks/Modules`; EF interceptors to `BuildingBlocks/Persistence/Interceptors`; pagination to `BuildingBlocks/Results/Pagination`; HTTP Result mappings to `BuildingBlocks/Http`. Keep pure Result/Error types in Results.
    - Reference JWT Bearer from Api and PostgreSQL/Npgsql from modules that use it. BuildingBlocks must not install either host authentication middleware or a specific database provider.
3. **NO Cross-Module Database Foreign Keys**:
    - Each module owns a dedicated PostgreSQL schema (`identity`, `profiles`, `catalog`, `booking`, etc.).
    - Cross-module relationships MUST only store unconstrained IDs (e.g. `public Guid MentorId { get; private set; }`). NEVER configure an EF Core Foreign Key pointing to a table in another module's schema.
4. **NO Throwing Exceptions for Business Flow**:
    - Do NOT throw exceptions for predictable business failures (e.g. `UserNotFoundException`).
    - ALWAYS return `Result` or `Result<T>` using `Error.NotFound()`, `Error.Validation()`, `Error.Conflict()`.
5. **NO Public Setters on Domain Entities**:
    - Encapsulate all state changes. Use `private set` or `init` and mutate state via expressive domain methods or factory methods (`Create(...)`).
    - Always `builder.Ignore(e => e.DomainEvents)` in EF Core entity configurations.

---

## 2. Standard 4-Tier Internal Module Structure

Inside any module `src/Modules/{ModuleName}/`:

```text
├── Domain/                         # Entities, Value Objects, Domain Events, Domain Errors
├── Application/
│   └── {Feature}/                  # Business group, e.g. Auth or Users in Identity
│       ├── {UseCase}/{UseCase}.cs   # Internal command/query, validator and handler
│       ├── Requests/               # HTTP body/query inputs, named *Request
│       └── Responses/              # API outputs, named *Response
├── Infrastructure/
│   └── Persistence/
│       ├── {Name}DbContext.cs
│       ├── Configurations/         # IEntityTypeConfiguration<T> (Fluent API)
│       └── Migrations/             # EF Core migrations; preserve migration IDs/schema
├── Endpoints/                      # Minimal API endpoint mappings
└── {Name}Module.cs                 # Inherits ModuleDefinition, registers DI & maps routes
```

Endpoints MUST bind HTTP inputs to Request DTOs and map them to internal Commands/Queries before calling MediatR. Do NOT bind Commands/Queries directly from HTTP. Keep request/response JSON fields, query parameter names and defaults unchanged when refactoring. Server-derived values such as the login IP address MUST come from `HttpContext`, not a client-supplied Request property.

Keep Requests/Responses under their feature group; for example `Application/Auth/Requests/LoginRequest.cs`, `Application/Auth/Login/Login.cs` and `Application/Auth/Responses/AuthResponse.cs`. Preserve type names such as `LoginCommand` inside the use-case file. Create directories only when code exists; do not scaffold empty layers or module-specific Security folders without an implementation.

Architecture tests belong to `tests/ArchitectureTests/SkillBridge.ArchitectureTests.csproj`; endpoint and security configuration tests belong to `tests/IntegrationTests/SkillBridge.IntegrationTests.csproj`.

---

## 3. C# 13 & .NET 10 Coding Standards

- **File-Scoped Namespaces**: Use the `SkillBridge.Modules.{Name}` prefix followed by the source folders, e.g. `SkillBridge.Modules.Identity.Application.Auth.Login` (do not use bracketed namespaces).
- **Collection Expressions**: Prefer `List<IModule> modules = [ new IdentityModule() ];` over `new List<IModule>()`.
- **Primary Constructors**: Use primary constructors for dependency injection where appropriate.
- **Fluent API**: Keep domain classes pure. Put all EF Core mappings inside `IEntityTypeConfiguration<T>` in `Infrastructure/Persistence/Configurations/`.
- **Migration History Table**: ALWAYS specify `npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "{schema_name}")`.

---

## 4. Standard Code Templates

### A. Domain Entity Template

```csharp
namespace SkillBridge.Modules.{ModuleName}.Domain;

using SkillBridge.BuildingBlocks.Domain;

public sealed class {EntityName} : AggregateRoot<Guid>
{
    public string Title { get; private set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private {EntityName}() { }

    public static {EntityName} Create(string title)
    {
        var entity = new {EntityName}
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        // entity.RaiseDomainEvent(new {EntityName}CreatedDomainEvent(entity.Id));
        return entity;
    }
}
```

### B. EF Core Entity Configuration Template

```csharp
namespace SkillBridge.Modules.{ModuleName}.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Modules.{ModuleName}.Domain;

internal sealed class {EntityName}Configuration : IEntityTypeConfiguration<{EntityName}>
{
    public void Configure(EntityTypeBuilder<{EntityName}> builder)
    {
        builder.ToTable("{table_name_plural}");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Ignore(e => e.DomainEvents);
    }
}
```

### C. Module Definition Template

```csharp
namespace SkillBridge.Modules.{ModuleName};

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.BuildingBlocks.Modules;
using SkillBridge.Modules.{ModuleName}.Infrastructure.Persistence;

public sealed class {ModuleName}Module : ModuleDefinition
{
    public override string Name => "{ModuleName}";
    public override string RoutePrefix => "{route_prefix}";

    public override void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' was not found.");

        services.AddDbContext<{ModuleName}DbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", {ModuleName}DbContext.Schema);
            });
        });
    }

    public override void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        base.MapEndpoints(endpoints); // Registers GET /api/v1/{route_prefix}/_module probe

        var group = endpoints.MapGroup($"/api/v1/{RoutePrefix}").WithTags(Name);
        // Map feature endpoints here
    }
}
```
