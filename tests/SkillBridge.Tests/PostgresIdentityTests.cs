using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SkillBridge.Modules.Identity.Application;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SKILLBRIDGE_TEST_DATABASE")))
            Skip = "Set SKILLBRIDGE_TEST_DATABASE to a PostgreSQL connection with CREATEDB permission.";
    }
}

public sealed class PostgresIdentityTests
{
    [PostgresFact]
    public async Task Authentication_rotation_ownership_lockout_and_admin_revocation()
    {
        var databaseName = $"sb_tests_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SKILLBRIDGE_TEST_DATABASE"));
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await create.ExecuteNonQueryAsync();
        connection.Database = databaseName;
        try
        {
            await using var factory = new ApiFactory(connection.ConnectionString);
            using var client = factory.CreateClient();
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                await db.Database.MigrateAsync();
                Assert.False(db.Database.HasPendingModelChanges());
            }
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);

            var registration = new RegisterRequest("User@example.com", "Password123!", "User", "Mentee");
            Assert.Equal(HttpStatusCode.Created,
                (await client.PostAsJsonAsync("/api/v1/identity/auth/register", registration)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,
                (await client.PostAsJsonAsync("/api/v1/identity/auth/register", registration with { Email = " user@example.com " })).StatusCode);

            async Task<AuthenticationResponse> Login(string email = "user@example.com")
            {
                var response = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new LoginRequest(email, "Password123!"));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(response.Headers.CacheControl?.NoStore);
                return (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
            }
            var first = await Login();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/identity/users/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/identity/users")).StatusCode);
            var rotatedResponse = await client.PostAsJsonAsync("/api/v1/identity/auth/refresh",
                new RefreshRequest(first.RefreshToken, first.SessionId));
            Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode);
            var rotated = (await rotatedResponse.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
            Assert.NotEqual(first.RefreshToken, rotated.RefreshToken);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/identity/auth/refresh",
                new RefreshRequest(first.RefreshToken, first.SessionId))).StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rotated.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/identity/users/me")).StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/identity/auth/refresh",
                new RefreshRequest(rotated.RefreshToken, rotated.SessionId))).StatusCode);

            // Two concurrent rotations must never both succeed.
            var concurrent = await Login();
            var refreshes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
                client.PostAsJsonAsync("/api/v1/identity/auth/refresh", new RefreshRequest(concurrent.RefreshToken, concurrent.SessionId))));
            Assert.Single(refreshes, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(refreshes, x => x.StatusCode == HttpStatusCode.Conflict);

            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/identity/auth/register",
                registration with { Email = "other@example.com" })).StatusCode);
            var other = await Login("other@example.com");
            var own = await Login();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", own.AccessToken);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/sessions/{other.SessionId}")).StatusCode);
            var anotherOwn = await Login();
            Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync("/api/v1/sessions/other")).StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", anotherOwn.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/identity/users/me")).StatusCode);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", own.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/identity/auth/change-password",
                new ChangePasswordRequest("Password123!", "Changed123!", "Changed123!"))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/identity/users/me")).StatusCode);

            for (var i = 0; i < 4; i++)
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/identity/auth/login",
                    new LoginRequest("other@example.com", "Wrong123!"))).StatusCode);
            Assert.Equal(HttpStatusCode.Locked, (await client.PostAsJsonAsync("/api/v1/identity/auth/login",
                new LoginRequest("other@example.com", "Wrong123!"))).StatusCode);

            // Provision a test administrator directly; public registration never grants this role.
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                await db.Users.Where(x => x.Id == own.User.Id).ExecuteUpdateAsync(
                    setter => setter.SetProperty(x => x.Roles, new[] { "SuperAdmin" }));
                var stored = await db.RefreshTokens.AsNoTracking().ToListAsync();
                Assert.DoesNotContain(stored, x => x.TokenHash == first.RefreshToken);
                Assert.Equal(2, await db.OutboxMessages.CountAsync());
            }
            // Authenticate the separately provisioned administrator with the changed password.
            using var adminClient = factory.CreateClient();
            adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                (await (await adminClient.PostAsJsonAsync("/api/v1/identity/auth/login",
                    new LoginRequest("user@example.com", "Changed123!"))).Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync($"/api/v1/identity/users/{other.User.Id}/status",
                new SetStatusRequest("Locked", "integration test"))).StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", other.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/identity/users/me")).StatusCode);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
