using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillBridge.BuildingBlocks.Pagination;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Application.Commands.Login;
using SkillBridge.Modules.Identity.Application.Commands.RefreshToken;
using SkillBridge.Modules.Identity.Application.Commands.Register;
using SkillBridge.Modules.Identity.Application.DTOs.Responses;
using SkillBridge.Modules.Identity.Application.Queries.GetCurrentUser;
using SkillBridge.Modules.Identity.Application.Queries.GetUsers;
using SkillBridge.Modules.Identity.Endpoints;

namespace SkillBridge.Tests.Identity;

public sealed class IdentityApiFixture : IAsyncLifetime
{
    public static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000017");
    public const string RemoteIp = "203.0.113.41";
    public HttpClient Client { get; private set; } = null!;
    public RecordingSender Sender { get; private set; } = null!;
    private WebApplication _app = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("Fixture")
            .AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("Fixture", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, FixtureCurrentUser>();
        var sender = DispatchProxy.Create<ISender, RecordingSender>();
        Sender = (RecordingSender)sender;
        builder.Services.AddSingleton(sender);
        _app = builder.Build();
        _app.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(RemoteIp);
            await next(context);
        });
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapAuthEndpoints();
        _app.MapUsersEndpoints();
        await _app.StartAsync();
        Client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }

    public class RecordingSender : DispatchProxy
    {
        public object? LastRequest { get; set; }
        public Result<AuthResponse>? AuthResultOverride { get; set; }

        protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(ISender.Send) || args?[0] is not { } request)
                throw new NotSupportedException("The fixture only supports sending requests.");

            LastRequest = request;
            return request switch
            {
                RegisterUserCommand or LoginCommand or RefreshTokenCommand =>
                    Task.FromResult(AuthResultOverride ?? Result<AuthResponse>.Success(new AuthResponse(
                        UserId, "fixture@example.test", "Fixture User", "Mentee",
                        "fixture-access-token", "fixture-refresh-token", 900))),
                GetCurrentUserQuery => Task.FromResult(Result<UserResponse>.Success(User())),
                GetUsersQuery => Task.FromResult(Result<PagedResult<UserResponse>>.Success(
                    PagedResult<UserResponse>.Create([User()], 1, 20, 1))),
                _ => Task.FromResult(Result.Success())
            };
        }

        private static UserResponse User() => new(
            UserId, "fixture@example.test", "Fixture User", "Mentee", null, null,
            false, true, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    }

    private sealed class FixtureCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        public Guid? UserId => accessor.HttpContext!.Request.Headers.ContainsKey("X-No-Current-User")
            ? null : IdentityApiFixture.UserId;
        public string? Email => "fixture@example.test";
        public string? Role => "Admin";
        public bool IsAuthenticated => UserId.HasValue;
        public bool HasRole(string role) => role == Role;
    }

    private sealed class FixtureAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Task.FromResult(AuthenticateResult.NoResult());

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, UserId.ToString())], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
