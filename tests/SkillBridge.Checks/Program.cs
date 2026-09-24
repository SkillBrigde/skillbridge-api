using System.Text.Json;
using System.Xml.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.BuildingBlocks.Behaviors;
using SkillBridge.BuildingBlocks.Domain;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Catalog;
using SkillBridge.Modules.Catalog.Application;
using SkillBridge.Modules.Catalog.Domain;
using SkillBridge.Modules.Catalog.Infrastructure;
using SkillBridge.Modules.Identity;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Checks;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            Check(args.Length == 0 || args.SequenceEqual(["--database"]), "Usage: dotnet run --project tests/SkillBridge.Checks [-- --database]");
            await CheckResultsAsync();
            CheckDomain();
            CheckArchitecture();
            CheckModelsAndMigrations();
            Console.WriteLine("PASS: validation, HTTP errors, JSON, domain, boundaries and PostgreSQL migration SQL.");
            if (args.Contains("--database")) await DatabaseChecks.RunAsync();
            else Console.WriteLine("SKIP: database/HTTP checks (use --database and SKILLBRIDGE_TEST_DATABASE).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
    }

    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task CheckResultsAsync()
    {
        var validator = new InlineValidator<Probe>();
        validator.RuleFor(request => request.Name).NotEmpty();
        var called = false;
        var behavior = new ValidationBehavior<Probe, Result<string>>([validator]);
        var invalid = await behavior.Handle(new Probe(""), _ =>
        {
            called = true;
            return Task.FromResult(Result.Success("unexpected"));
        }, default);
        Check(invalid.IsFailure && invalid.Error is ValidationError && !called,
            "Generic validation must return a validation error without invoking the handler.");
        var valid = await behavior.Handle(new Probe("valid"), _ => Task.FromResult(Result.Success("ok")), default);
        Check(valid.IsSuccess && valid.Value == "ok", "Valid request did not reach the handler.");
        var nonGeneric = await new ValidationBehavior<Probe, Result>([validator])
            .Handle(new Probe(""), _ => Task.FromResult(Result.Success()), default);
        Check(nonGeneric.IsFailure, "Non-generic Result validation must fail without throwing.");

        foreach (var (error, expected) in new[]
        {
            (Error.Validation("validation", "invalid"), 400),
            (Error.Unauthorized("auth", "unauthorized"), 401),
            (Error.Forbidden("access", "forbidden"), 403),
            (Error.NotFound("missing", "not found"), 404),
            (Error.Conflict("duplicate", "conflict"), 409)
        })
            Check(((IStatusCodeHttpResult)error.ToProblemDetails()).StatusCode == expected, $"Incorrect HTTP mapping for {error.Type}.");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(PagedResult<int>.Create([1], 2, 10, 11), JsonSerializerOptions.Web));
        Check(json.RootElement.GetProperty("page").GetInt32() == 2 && !json.RootElement.TryGetProperty("pageNumber", out _),
            "PagedResult must serialize page, as required by the API contract.");
        Check(json.RootElement.GetProperty("totalPages").GetInt32() == 2, "Incorrect totalPages.");
    }

    private static void CheckDomain()
    {
        var category = Category.Create("  Lập trình Đám mây!  ", null, null, null, 0);
        Check(category.IsSuccess && category.Value.Slug == "lap-trinh-dam-may", "Vietnamese slug normalization failed.");
        foreach (var name in new string?[] { null, "", "   ", "🚀", "a\0b", new('a', 151), "\ud800" })
            Check(Tag.Create(name).IsFailure, "Invalid tag name accepted.");
        Check(category.Value.Update("Updated", null, category.Value.Id, null, 0, true).IsFailure, "Self-parent accepted.");
        Check(category.Value.Name == "Lập trình Đám mây!", "Failed category update mutated entity.");
        Check(Category.Create("Bad URL", null, null, "javascript:alert(1)", 0).IsFailure, "Unsafe icon scheme accepted.");
        Check(Skill.Create(Guid.Empty, "Skill", null, []).IsFailure, "Empty category accepted.");
        var tag = Tag.Create("Backend").Value;
        var skill = Skill.Create(category.Value.Id, "C#", null, [tag, tag]).Value;
        Check(skill.Tags.Count == 1, "Duplicate tag links accepted.");
        category.Value.Delete();
        Check(category.Value.IsDeleted && !category.Value.IsActive && category.Value.DeletedAtUtc.HasValue, "Soft deletion is incomplete.");
        Check(CatalogPagination.Offset(2, 10).Value == 10 && CatalogPagination.Offset(int.MaxValue, 100).IsFailure &&
            CatalogPagination.Offset(0, 10).IsFailure && CatalogPagination.Offset(1, 101).IsFailure, "Pagination boundary/overflow validation failed.");

        var user = User.Create(" Example@Mail.test ", "hash", " Example User ");
        Check(user.NormalizedEmail == "EXAMPLE@MAIL.TEST", "Email normalization failed.");
        for (var i = 0; i < 5; i++) user.RecordFailedLogin();
        Check(user.IsLockedOut, "Five failed logins did not lock account.");
        user.ResetFailedLogin();
        Check(!user.IsLockedOut && user.AccessFailedCount == 0, "Successful login did not reset lockout.");
        var stamp = user.SecurityStamp;
        user.ChangePassword("new hash");
        Check(stamp != user.SecurityStamp, "Password change did not invalidate security stamp.");
        stamp = user.SecurityStamp;
        user.SetStatus(false);
        Check(stamp != user.SecurityStamp, "Disabling user did not invalidate security stamp.");

        var token = RefreshToken.Create(user.Id, "secret", DateTimeOffset.UtcNow.AddMinutes(1), user.SecurityStamp);
        Check(token.IsActive && token.Token.Length == 64 && token.Token != "secret" && token.Token == RefreshToken.Hash("secret"),
            "Refresh token must be stored as SHA-256 hash.");
        token.Revoke("replacement");
        Check(!token.IsActive && token.ReplacedByToken == RefreshToken.Hash("replacement"), "Rotation must hash replacement and revoke old token.");
        Check(!RefreshToken.Create(user.Id, "expired", DateTimeOffset.UtcNow.AddMinutes(-1), user.SecurityStamp).IsActive,
            "Expired refresh token accepted.");
        var hasher = new PasswordHasher();
        var hash = hasher.HashPassword("Password123!");
        Check(hasher.VerifyPassword("Password123!", hash) && !hasher.VerifyPassword("incorrect", hash), "Password verification failed.");
        Check(!hasher.VerifyPassword("Password123!", "invalid-hash"), "Malformed stored hash must fail closed.");
    }

    private static void CheckArchitecture()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "SkillBridge.slnx"))) root = root.Parent;
        Check(root is not null, "Cannot locate repository for architecture checks.");
        var modules = Path.Combine(root!.FullName, "src", "Modules");
        foreach (var path in Directory.GetFiles(modules, "*.csproj", SearchOption.AllDirectories))
        {
            var references = XDocument.Load(path).Descendants("ProjectReference").Select(element => (string?)element.Attribute("Include"));
            foreach (var reference in references)
            {
                var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, reference!.Replace('\\', Path.DirectorySeparatorChar)));
                Check(!target.StartsWith(modules + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), $"Cross-module reference in {Path.GetFileName(path)}.");
            }
        }
        Check(!typeof(Result).Assembly.GetReferencedAssemblies().Any(assembly => assembly.Name!.StartsWith("SkillBridge.Modules.")),
            "BuildingBlocks references a business module.");
    }

    private static void CheckModelsAndMigrations()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Host=localhost;Database=unused;Username=unused;Password=unused"
        }).Build();
        var services = new ServiceCollection().AddLogging();
        new IdentityModule().AddServices(services, config);
        new CatalogModule().AddServices(services, config);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        CheckModel(scope.ServiceProvider.GetRequiredService<IdentityDbContext>(), "identity");
        CheckModel(scope.ServiceProvider.GetRequiredService<CatalogDbContext>(), "catalog");
    }

    private static void CheckModel(DbContext context, string schema)
    {
        foreach (var entity in context.Model.GetEntityTypes())
        {
            Check(entity.GetSchema() == schema, $"{entity.Name} escaped its module schema.");
            if (typeof(IHasDomainEvents).IsAssignableFrom(entity.ClrType))
                Check(entity.FindProperty("DomainEvents") is null && entity.FindNavigation("DomainEvents") is null, "DomainEvents was persisted.");
            foreach (var foreignKey in entity.GetForeignKeys())
                Check(foreignKey.PrincipalEntityType.GetSchema() == schema, "Cross-module foreign key found.");
        }
        var sql = context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Check(sql.Contains($"{schema}.\"__EFMigrationsHistory\"", StringComparison.Ordinal) && sql.Contains("CREATE TABLE", StringComparison.Ordinal),
            $"Missing {schema} migrations or schema-local history table.");
    }

    private sealed record Probe(string Name);
}
