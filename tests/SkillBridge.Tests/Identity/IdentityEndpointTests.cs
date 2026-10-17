using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Net.Http.Headers;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Application.Commands.ChangePassword;
using SkillBridge.Modules.Identity.Application.Commands.Login;
using SkillBridge.Modules.Identity.Application.Commands.Logout;
using SkillBridge.Modules.Identity.Application.Commands.RefreshToken;
using SkillBridge.Modules.Identity.Application.Commands.Register;
using SkillBridge.Modules.Identity.Application.Commands.UpdateUserStatus;
using SkillBridge.Modules.Identity.Application.DTOs.Responses;
using SkillBridge.Modules.Identity.Application.Queries.GetCurrentUser;
using SkillBridge.Modules.Identity.Application.Queries.GetUsers;

namespace SkillBridge.Tests.Identity;

public sealed class IdentityEndpointTests(IdentityApiFixture api) : IClassFixture<IdentityApiFixture>
{
    private const string AuthPath = "/api/v1/identity/auth/";
    private const string UsersPath = "/api/v1/identity/users/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [MemberData(nameof(EndpointCases))]
    public async Task Requests_preserve_binding_mapping_and_authorization(
        string name, string method, string path, string? body, string? cookie,
        bool authorized, bool noCurrentUser, int expectedStatus,
        string? expectedCommandType, string? expectedCommandJson)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        if (cookie != null) request.Headers.Add("Cookie", cookie);
        if (authorized) request.Headers.Add("Authorization", "Fixture test");
        if (noCurrentUser) request.Headers.Add("X-No-Current-User", "true");
        api.Sender.LastRequest = null;

        using var response = await api.Client.SendAsync(request);

        Assert.True((int)response.StatusCode == expectedStatus,
            $"{name}: expected {expectedStatus}, received {(int)response.StatusCode}.");
        Assert.Equal(expectedCommandType, api.Sender.LastRequest?.GetType().Name);
        if (expectedCommandJson != null)
        {
            var actual = JsonSerializer.SerializeToNode(api.Sender.LastRequest, JsonOptions);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expectedCommandJson), actual),
                $"{name}: the endpoint sent an unexpected command/query: {actual}");
        }
    }

    public static IEnumerable<object[]> EndpointCases()
    {
        const string email = "fixture@example.test";
        const string password = "Fixture1!";
        var id = IdentityApiFixture.UserId;
        var ip = IdentityApiFixture.RemoteIp;
        yield return Case("register defaults", "POST", AuthPath + "register",
            """{"email":"fixture@example.test","password":"Fixture1!","fullName":"Fixture User"}""",
            new RegisterUserCommand(email, password, "Fixture User"));
        yield return Case("register optional fields", "POST", AuthPath + "register",
            """{"email":"fixture@example.test","password":"Fixture1!","fullName":"Fixture User","role":"Mentor","phoneNumber":"010101"}""",
            new RegisterUserCommand(email, password, "Fixture User", "Mentor", "010101"));
        yield return Case("register explicit null role", "POST", AuthPath + "register",
            """{"email":"fixture@example.test","password":"Fixture1!","fullName":"Fixture User","role":null}""",
            new RegisterUserCommand(email, password, "Fixture User", null!));
        yield return Case("missing fields reach command validation", "POST", AuthPath + "register", "{}",
            new RegisterUserCommand(null!, null!, null!));
        yield return Case("malformed JSON", "POST", AuthPath + "register", "{", status: 400);
        yield return Case("login overrides client IP", "POST", AuthPath + "login",
            """{"email":"fixture@example.test","password":"Fixture1!","ipAddress":"198.51.100.1"}""",
            new LoginCommand(email, password, ip));
        yield return Case("login without client IP", "POST", AuthPath + "login",
            """{"email":"fixture@example.test","password":"Fixture1!"}""", new LoginCommand(email, password, ip));

        foreach (var operation in new[] { "refresh", "logout" })
        {
            object Command(string? token) => operation == "refresh"
                ? new RefreshTokenCommand(token ?? "", ip) : new LogoutCommand(token);
            const string cookie = "__Host-refresh=fixture-cookie-token";
            yield return Case(operation + " body overrides cookie", "POST", AuthPath + operation,
                """{"refreshToken":"fixture-body-token"}""", Command("fixture-body-token"), cookie);
            yield return Case(operation + " empty field uses cookie", "POST", AuthPath + operation,
                """{"refreshToken":""}""", Command("fixture-cookie-token"), cookie);
            yield return Case(operation + " null field uses cookie", "POST", AuthPath + operation,
                """{"refreshToken":null}""", Command("fixture-cookie-token"), cookie);
            yield return Case(operation + " null body uses cookie", "POST", AuthPath + operation,
                "null", Command("fixture-cookie-token"), cookie);
            yield return Case(operation + " absent body uses cookie", "POST", AuthPath + operation,
                null, Command("fixture-cookie-token"), cookie);
            yield return Case(operation + " absent body and cookie", "POST", AuthPath + operation,
                null, Command(null));
        }

        const string changePassword = """{"currentPassword":"FixtureOld1!","newPassword":"FixtureNew1!"}""";
        yield return Case("password mapping", "POST", AuthPath + "change-password", changePassword,
            new ChangePasswordCommand(id, "FixtureOld1!", "FixtureNew1!"));
        yield return Case("password anonymous", "POST", AuthPath + "change-password", changePassword,
            authorized: false, status: 401);
        yield return Case("password missing user ID", "POST", AuthPath + "change-password", changePassword,
            noCurrentUser: true, status: 401);
        yield return Case("current user", "GET", UsersPath + "me", null, new GetCurrentUserQuery(id));
        yield return Case("current user anonymous", "GET", UsersPath + "me", null, authorized: false, status: 401);
        yield return Case("current user missing ID", "GET", UsersPath + "me", null, noCurrentUser: true, status: 401);
        yield return Case("users defaults", "GET", UsersPath, null, new GetUsersQuery());
        yield return Case("users all query fields", "GET",
            UsersPath + "?page=3&pageSize=7&search=fixture&role=Mentor&isActive=false", null,
            new GetUsersQuery(3, 7, "fixture", "Mentor", false));
        yield return Case("query names ignore case", "GET",
            UsersPath + "?PAGE=2&PAGESIZE=10&SEARCH=fixture&ROLE=Mentee&ISACTIVE=true", null,
            new GetUsersQuery(2, 10, "fixture", "Mentee", true));
        yield return Case("empty numeric and boolean query values", "GET",
            UsersPath + "?page=&pageSize=&search=&role=&isActive=", null, status: 400);
        yield return Case("invalid page", "GET", UsersPath + "?page=bad", null, status: 400);
        yield return Case("invalid active filter", "GET", UsersPath + "?isActive=bad", null, status: 400);
        yield return Case("unknown query names ignored", "GET", UsersPath + "?pageNumber=8&searchTerm=ignored",
            null, new GetUsersQuery());
        yield return Case("pagination normalization belongs to handler", "GET", UsersPath + "?page=-3&pageSize=200",
            null, new GetUsersQuery(-3, 200));
        yield return Case("users anonymous", "GET", UsersPath, null, authorized: false, status: 401);
        yield return Case("disable user", "PUT", UsersPath + id + "/status", """{"isActive":false}""",
            new UpdateUserStatusCommand(id, false));
        yield return Case("enable user", "PUT", UsersPath + id + "/status", """{"isActive":true}""",
            new UpdateUserStatusCommand(id, true));
        yield return Case("status boolean default", "PUT", UsersPath + id + "/status", "{}",
            new UpdateUserStatusCommand(id, false));
        yield return Case("invalid user route", "PUT", UsersPath + "invalid/status", """{"isActive":true}""", status: 404);
        yield return Case("status anonymous", "PUT", UsersPath + id + "/status", """{"isActive":true}""",
            authorized: false, status: 401);
    }

    private static object[] Case(string name, string method, string path, string? body,
        object? command = null, string? cookie = null, bool authorized = true,
        bool noCurrentUser = false, int status = 200) =>
        [name, method, path, body!, cookie!, authorized, noCurrentUser, status,
            command?.GetType().Name!, command == null ? null! : JsonSerializer.Serialize(command, JsonOptions)];

    [Theory]
    [InlineData("register")]
    [InlineData("login")]
    [InlineData("refresh")]
    public async Task Authentication_responses_keep_the_public_JSON_contract(string operation)
    {
        var body = operation == "refresh" ? """{"refreshToken":"fixture-token"}"""
            : """{"email":"fixture@example.test","password":"Fixture1!","fullName":"Fixture User"}""";
        using var response = await api.Client.PostAsync(AuthPath + operation,
            new StringContent(body, Encoding.UTF8, "application/json"));
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(["accessToken", "email", "expiresIn", "fullName", "refreshToken", "role", "userId"],
            json.Select(property => property.Key).Order().ToArray());
        Assert.Equal("fixture-access-token", json["accessToken"]!.GetValue<string>());
        Assert.Equal(900, json["expiresIn"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("me", false)]
    [InlineData("", true)]
    public async Task User_responses_keep_the_public_JSON_contract(string path, bool paged)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsersPath + path);
        request.Headers.Add("Authorization", "Fixture test");
        using var response = await api.Client.SendAsync(request);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        if (paged)
        {
            Assert.Equal(["hasNextPage", "hasPreviousPage", "items", "pageNumber", "pageSize", "totalCount", "totalPages"],
                body.AsObject().Select(property => property.Key).Order().ToArray());
        }
        var user = (paged ? body["items"]![0]! : body).AsObject();

        Assert.Equal(["avatarUrl", "createdAtUtc", "email", "fullName", "id", "isActive", "isEmailConfirmed", "phoneNumber", "role"],
            user.Select(property => property.Key).Order().ToArray());
        Assert.Equal(IdentityApiFixture.UserId, user["id"]!.GetValue<Guid>());
        Assert.Null(user["phoneNumber"]);
    }

    [Theory]
    [InlineData("login")]
    [InlineData("refresh")]
    public async Task Login_and_refresh_set_the_secure_refresh_cookie(string operation)
    {
        var before = DateTimeOffset.UtcNow;
        using var response = await api.Client.PostAsync(AuthPath + operation,
            new StringContent("{}", Encoding.UTF8, "application/json"));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.StartsWith("__Host-refresh=fixture-refresh-token;", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        var expires = SetCookieHeaderValue.Parse(cookie).Expires;
        Assert.NotNull(expires);
        Assert.InRange(expires.Value, before.AddDays(7).AddSeconds(-1), DateTimeOffset.UtcNow.AddDays(7).AddSeconds(1));
    }

    [Theory]
    [InlineData("refresh")]
    [InlineData("logout")]
    public async Task Whitespace_body_token_takes_precedence_over_the_cookie(string operation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, AuthPath + operation)
        {
            Content = new StringContent("""{"refreshToken":"  "}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Cookie", "__Host-refresh=fixture-cookie-token");
        request.Headers.Add("Authorization", "Fixture test");

        using var response = await api.Client.SendAsync(request);

        Assert.Equal(200, (int)response.StatusCode);
        if (operation == "refresh")
            Assert.Equal("  ", Assert.IsType<RefreshTokenCommand>(api.Sender.LastRequest).RefreshToken);
        else
            Assert.Equal("  ", Assert.IsType<LogoutCommand>(api.Sender.LastRequest).RefreshToken);
    }

    [Theory]
    [InlineData("login", "failure", 400)]
    [InlineData("refresh", "failure", 400)]
    [InlineData("login", "empty token", 200)]
    [InlineData("refresh", "empty token", 200)]
    public async Task Failed_authentication_or_an_empty_returned_token_does_not_set_a_refresh_cookie(
        string operation, string outcome, int expectedStatus)
    {
        var previous = api.Sender.AuthResultOverride;
        try
        {
            api.Sender.AuthResultOverride = outcome == "failure"
                ? Result<AuthResponse>.Failure(Error.Validation("Fixture.Invalid", "Fixture rejection"))
                : Result<AuthResponse>.Success(new AuthResponse(
                    IdentityApiFixture.UserId, "fixture@example.test", "Fixture User", "Mentee",
                    "fixture-access-token", "", 900));

            using var response = await api.Client.PostAsync(AuthPath + operation,
                new StringContent("{}", Encoding.UTF8, "application/json"));

            Assert.Equal(expectedStatus, (int)response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
        finally
        {
            api.Sender.AuthResultOverride = previous;
        }
    }

    [Fact]
    public async Task Logout_expires_the_refresh_cookie()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, AuthPath + "logout");
        request.Headers.Add("Authorization", "Fixture test");
        using var response = await api.Client.SendAsync(request);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.Equal(200, (int)response.StatusCode);
        Assert.StartsWith("__Host-refresh=;", cookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie);
    }
}
