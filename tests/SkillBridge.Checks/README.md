# Runnable backend checks

```powershell
dotnet run --project tests/SkillBridge.Checks
```

Runs domain/validation/serialization checks, module-reference checks, EF schema/FK checks and generation of PostgreSQL migration SQL. Requires no running database and uses no test framework.

```powershell
# Set SKILLBRIDGE_TEST_DATABASE to a PostgreSQL connection string using your local secret configuration.
dotnet run --project tests/SkillBridge.Checks -- --database
```

The database account must have `CREATEDB` permission. This mode creates a uniquely named `skillbridge_checks_*` database, applies migrations, exercises the real module endpoints through local Kestrel, then removes only that database. It never migrates or clears the database named in the supplied connection string.

The HTTP host reuses module service/route registration and shared authentication/middleware. It checks Identity authorization, secure refresh cookies, concurrent rotation, password/status revocation, Catalog CRUD/tree/uniqueness/concurrent cycle protection, and Profiles Mentor/Mentee self-service authorization, validation, creation and update. It also checks Scheduling, multi-session Booking/Learning, Payments signatures/ledger/recovery, Reviews evidence/ownership/edit window, and Recommendations real metrics/ranking/deduplication. Gateway HTTP tests use a PayOS stub and signed VNPAY fixtures; they do not transfer real money. Background workers are called deterministically in this harness. Actual Api entry point startup is verified separately against an isolated empty database.
