using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SkillBridge.Modules.Identity.Infrastructure.Persistence;

namespace SkillBridge.IntegrationTests.Persistence;

public sealed class IdentityMigrationTests
{
    [Fact]
    public void Migration_snapshot_matches_the_current_Identity_model()
    {
        using var context = Context();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Completing_Identity_schema_adds_missing_structures_without_dropping_existing_user_data()
    {
        using var context = Context();
        var migrations = context.Database.GetMigrations().ToArray();
        var initial = Assert.Single(migrations, migration => migration.EndsWith("_Initial_Identity", StringComparison.Ordinal));
        var complete = Assert.Single(migrations, migration => migration.EndsWith("_CompleteIdentitySchema", StringComparison.Ordinal));
        var script = context.GetService<IMigrator>().GenerateScript(initial, complete);
        var sql = NormalizeSql(script);

        foreach (var table in new[] { "EXTERNAL_LOGINS", "REFRESH_TOKENS", "ROLES", "USER_ROLES" })
            Assert.Matches(@"\bCREATE TABLE IDENTITY\." + table + @"\b", sql);

        foreach (var column in new[]
        {
            "NORMALIZEDEMAIL", "PASSWORDHASH", "SECURITYSTAMP", "PHONENUMBER", "AVATARURL",
            "ISEMAILCONFIRMED", "ISPHONECONFIRMED", "TWOFACTORENABLED", "LOCKOUTENDUTC",
            "LOCKOUTENABLED", "ACCESSFAILEDCOUNT", "UPDATEDATUTC"
        })
            Assert.Matches(@"\bALTER TABLE IDENTITY\.USERS ADD (?:COLUMN )?" + column + @"\b", sql);

        Assert.DoesNotMatch(@"\bDROP TABLE (?:IF EXISTS )?IDENTITY\.USERS\b", sql);
        foreach (var column in new[] { "ID", "EMAIL", "FULLNAME", "ROLE", "ISACTIVE", "CREATEDATUTC" })
            Assert.DoesNotMatch(@"\bDROP COLUMN (?:IF EXISTS )?" + column + @"\b", sql);
    }

    [Fact]
    public void Legacy_users_require_password_recovery_and_email_backfill_precedes_the_unique_index()
    {
        using var context = Context();
        var migrations = context.Database.GetMigrations().ToArray();
        var initial = Assert.Single(migrations, migration => migration.EndsWith("_Initial_Identity", StringComparison.Ordinal));
        var complete = Assert.Single(migrations, migration => migration.EndsWith("_CompleteIdentitySchema", StringComparison.Ordinal));
        var sql = NormalizeSql(context.GetService<IMigrator>().GenerateScript(initial, complete));

        Assert.Matches(@"\bUPDATE IDENTITY\.USERS\b.*?\bISACTIVE\s*=\s*FALSE\b", sql);
        Assert.Contains("!RESET_REQUIRED!", sql);
        var backfill = Regex.Match(sql, @"\bNORMALIZEDEMAIL\s*=");
        var uniqueIndex = Regex.Match(sql, @"\bCREATE UNIQUE INDEX IX_USERS_NORMALIZEDEMAIL\b");
        Assert.True(backfill.Success, "Existing users must receive a normalized email.");
        Assert.True(uniqueIndex.Success, "The normalized email uniqueness constraint must exist.");
        Assert.True(backfill.Index < uniqueIndex.Index, "Email backfill must complete before the unique index is created.");
    }

    private static string NormalizeSql(string sql) =>
        Regex.Replace(sql.Replace("\"", ""), @"\s+", " ").ToUpperInvariant();

    // Building the model and generating SQL never opens this fixture connection.
    private static IdentityDbContext Context() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=fixture_not_used;Username=fixture")
        .Options);
}
