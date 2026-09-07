# SkillBridge API

.NET 10 Modular Monolith for the SkillBridge mentoring platform.

## Architecture

- `src/Bootstrapper/SkillBridge.Api`: composition root, HTTP pipeline, health checks and SignalR.
- `src/BuildingBlocks/SkillBridge.BuildingBlocks`: shared technical primitives only.
- `src/Modules/*`: independently owned business modules.
- Each module owns its domain rules and, when persistence is added, its PostgreSQL schema.
- Modules do not reference one another directly. Cross-module workflows use contracts and integration events.

Initial modules: Identity, Profiles, Catalog, Booking, Scheduling, Payments, Learning, Messaging, Reviews and Recommendations.

## Run locally

```powershell
dotnet restore
dotnet build --configuration Release
dotnet run --project src/Bootstrapper/SkillBridge.Api
```

Open `http://localhost:8080/health/live` or any module probe such as `http://localhost:8080/api/v1/identity/_module`.

## Configuration

Use environment variables for secrets. Never commit local secrets. Nested .NET keys use double underscores, for example:

```text
ConnectionStrings__Database=Host=localhost;Port=5432;Database=skillbridge;Username=skillbridge;Password=...
RabbitMq__Host=localhost
```

## Module shape

When the first use case is added to a module, keep these folders inside the module project:

```text
Domain/          Entities, value objects, domain events and policies
Application/     Commands, queries, ports and orchestration
Infrastructure/  Persistence and external adapters
Endpoints/       HTTP contracts and endpoint mapping
```

See `docs/adr` for decisions that the team must preserve.
