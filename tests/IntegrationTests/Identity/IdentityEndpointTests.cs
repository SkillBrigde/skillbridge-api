using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Net.Http.Headers;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Identity.Application.Auth.ChangePassword;
using SkillBridge.Modules.Identity.Application.Auth.ForgotPassword;
using SkillBridge.Modules.Identity.Application.Auth.Login;
using SkillBridge.Modules.Identity.Application.Auth.Logout;
using SkillBridge.Modules.Identity.Application.Auth.RefreshToken;
using SkillBridge.Modules.Identity.Application.Auth.Register;
using SkillBridge.Modules.Identity.Application.Auth.ResetPassword;
using SkillBridge.Modules.Identity.Application.Auth.Responses;
using SkillBridge.Modules.Identity.Application.Users.GetCurrentUser;
using SkillBridge.Modules.Identity.Application.Users.GetUser;
using SkillBridge.Modules.Identity.Application.Users.GetUsers;
using SkillBridge.Modules.Identity.Application.Users.UpdateCurrentUser;
using SkillBridge.Modules.Identity.Application.Users.UpdateUserStatus;

namespace SkillBridge.IntegrationTests.Identity;

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
        var targetId = Guid.Parse("00000000-0000-0000-0000-000000000018");
        var ip = IdentityApiFixture.RemoteIp;
        yield return Case("register defaults", "POST", AuthPath + "register",
            """{"email":"fixture@example.test","password":"Fixture1!","fullName":"Fixture User"}""",
            new RegisterUserCommand(email, password, "Fixture User", "Mentee"), status: 201);
        yield return Case("register optional fields", "POST", AuthPath + "register",
            """{"email":"fixture@example.test","password":"Fixture1!","fullName":"Fixture User","role":"Mentor","phoneNumber":"010101"}""",
            new RegisterUserCommand(email, password, "Fixture User", "Mentor", "010101"), status: 201);
        yield return Case("register explicit null role reaches validation", "POST", AuthPath + "register",
            """{"email":"fixture@example.test","password":"Fixture1!","fullName":"Fixture User","role":null}""",
            new RegisterUserCommand(email, password, "Fixture User", null!), status: 201);
        yield return Case("missing fields reach validation", "POST", AuthPath + "register", "{}",
            new RegisterUserCommand(null!, null!, null!, "Mentee"), status: 201);
        yield return Case("malformed JSON", "POST", AuthPath + "register", "{", status: 400);
        yield return Case("login overrides client IP", "POST", AuthPath + "login",
            """{"email":"fixture@example.test","password":"Fixture1!","ipAddress":"198.51.100.1"}""",
            new LoginCommand(email, password, ip));
        yield return Case("login without client IP", "POST", AuthPath + "login",
            """{"email":"fixture@example.test","password":"Fixture1!"}""", new LoginCommand(email, password, ip));

        foreach (var operation in new[] { "refresh", "logout" })
        {
            object Command(string? token) => operation == "refresh"
                ? new RefreshTokenCommand(token ?? "", ip) : new LogoutCommand(id, token);
            var status = operation == "refresh" ? 200 : 204;
            const string cookie = "refreshToken=fixture-cookie-token";
            yield return Case(operation + " ignores body token", "POST", AuthPath + operation,
                """{"refreshToken":"fixture-body-token"}""", Command("fixture-cookie-token"), cookie, status: status);
            yield return Case(operation + " absent body uses cookie", "POST", AuthPath + operation,
                null, Command("fixture-cookie-token"), cookie, status: status);
            yield return Case(operation + " absent body and cookie", "POST", AuthPath + operation,
                null, Command(null), status: status);
            yield return Case(operation + " missing current user", "POST", AuthPath + operation,
                null, operation == "refresh" ? Command(null) : null, noCurrentUser: true,
                status: operation == "refresh" ? 200 : 401);
        }

        const string changePassword = """{"currentPassword":"FixtureOld1!","newPassword":"FixtureNew1!"}""";
        yield return Case("password mapping", "POST", AuthPath + "change-password", changePassword,
            new ChangePasswordCommand(id, "FixtureOld1!", "FixtureNew1!"), status: 204);
        yield return Case("password anonymous", "POST", AuthPath + "change-password", changePassword,
            authorized: false, status: 401);
        yield return Case("password missing user ID", "POST", AuthPath + "change-password", changePassword,
            noCurrentUser: true, status: 401);
        yield return Case("forgot password", "POST", AuthPath + "forgot-password", """{"email":"fixture@example.test"}""",
            new ForgotPasswordCommand(email), authorized: false, status: 204);
        yield return Case("reset password", "POST", AuthPath + "reset-password",
            """{"email":"fixture@example.test","token":"fixture-reset","newPassword":"FixtureNew1!"}""",
            new ResetPasswordCommand(email, "fixture-reset", "FixtureNew1!"), authorized: false, status: 204);
        yield return Case("logout anonymous", "POST", AuthPath + "logout", null, authorized: false, status: 401);
        yield return Case("current user", "GET", UsersPath + "me", null, new GetCurrentUserQuery(id));
        yield return Case("current user anonymous", "GET", UsersPath + "me", null, authorized: false, status: 401);
        yield return Case("current user missing ID", "GET", UsersPath + "me", null, noCurrentUser: true, status: 401);
        yield return Case("update current user", "PUT", UsersPath + "me", """{"fullName":"Updated User"}""",
            new UpdateCurrentUserCommand(id, "Updated User"), status: 204);
        yield return Case("update current user anonymous", "PUT", UsersPath + "me", """{"fullName":"Updated User"}""",
            authorized: false, status: 401);
        yield return Case("user details", "GET", UsersPath + targetId, null, new GetUserQuery(targetId));
        yield return Case("users defaults", "GET", UsersPath, null, new GetUsersQuery());
        yield return Case("users all query fields", "GET",
            UsersPath + "?page=3&pageSize=7&search=fixture&role=Mentor&isActive=false", null,
            new GetUsersQuery(3, 7, "fixture", "Mentor", false));
        yield return Case("legacy search parameter", "GET", UsersPath + "?searchTerm=fixture", null,
            new GetUsersQuery(SearchTerm: "fixture"));
        yield return Case("query names ignore case", "GET",
            UsersPath + "?PAGE=2&PAGESIZE=10&SEARCH=fixture&ROLE=Mentee&ISACTIVE=true", null,
            new GetUsersQuery(2, 10, "fixture", "Mentee", true));
        yield return Case("empty numeric and boolean query values", "GET",
            UsersPath + "?page=&pageSize=&isActive=", null, status: 400);
        yield return Case("invalid page", "GET", UsersPath + "?page=bad", null, status: 400);
        yield return Case("invalid active filter", "GET", UsersPath + "?isActive=bad", null, status: 400);
        yield return Case("unknown query names ignored", "GET", UsersPath + "?pageNumber=8", null, new GetUsersQuery());
        yield return Case("pagination validation belongs to handler", "GET", UsersPath + "?page=-3&pageSize=200",
            null, new GetUsersQuery(-3, 200));
        yield return Case("users anonymous", "GET", UsersPath, null, authorized: false, status: 401);
        yield return Case("disable user", "PUT", UsersPath + targetId + "/status", """{"isActive":false}""",
            new UpdateUserStatusCommand(targetId, false), status: 204);
        yield return Case("enable user", "PUT", UsersPath + targetId + "/status", """{"isActive":true}""",
            new UpdateUserStatusCommand(targetId, true), status: 204);
        yield return Case("status must be explicit", "PUT", UsersPath + targetId + "/status", "{}", status: 400);
        yield return Case("status cannot be null", "PUT", UsersPath + targetId + "/status", """{"isActive":null}""", status: 400);
        yield return Case("self deactivation rejected", "PUT", UsersPath + id + "/status", """{"isActive":false}""", status: 403);
        yield return Case("invalid user route", "PUT", UsersPath + "invalid/status", """{"isActive":true}""", status: 404);
        yield return Case("status anonymous", "PUT", UsersPath + targetId + "/status", """{"isActive":true}""",
            authorized: false, status: 401);
    }

    private static object[] Case(string name, string method, string path, string? body,
        object? command = null, string? cookie = null, bool authorized = true,
        bool noCurrentUser = false, int status = 200) =>
        [name, method, path, body!, cookie!, authorized, noCurrentUser, status,
            command?.GetType().Name!, command == null ? null! : JsonSerializer.Serialize(command, JsonOptions)];

    [Fact]
    public async Task Registration_returns_the_created_user_without_issuing_tokens()
    {
        using var response = await api.Client.PostAsync(AuthPath + "register",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(201, (int)response.StatusCode);
        Assert.Equal(["id"], json.Select(property => property.Key).ToArray());
        Assert.Equal(IdentityApiFixture.UserId, json["id"]!.GetValue<Guid>());
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("login")]
    [InlineData("refresh")]
    public async Task Authentication_responses_expose_only_access_token_and_lifetime(string operation)
    {
        using var response = await api.Client.PostAsync(AuthPath + operation,
            new StringContent("{}", Encoding.UTF8, "application/json"));
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(["accessToken", "expiresIn"], json.Select(property => property.Key).Order().ToArray());
        Assert.Equal("fixture-access-token", json["accessToken"]!.GetValue<string>());
        Assert.Equal(900, json["expiresIn"]!.GetValue<int>());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
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
            Assert.Equal(["items", "page", "pageSize", "totalCount", "totalPages"],
                body.AsObject().Select(property => property.Key).Order().ToArray());
        var user = (paged ? body["items"]![0]! : body).AsObject();
        var expected = paged
            ? new[] { "createdAtUtc", "email", "fullName", "id", "isActive", "role" }
            : ["email", "fullName", "id", "isActive", "role"];
        Assert.Equal(expected, user.Select(property => property.Key).Order().ToArray());
        Assert.Equal(IdentityApiFixture.UserId, user["id"]!.GetValue<Guid>());
    }

    [Theory]
    [InlineData("login")]
    [InlineData("refresh")]
    public async Task Login_and_refresh_set_the_secure_refresh_cookie(string operation)
    {
        using var response = await api.Client.PostAsync(AuthPath + operation,
            new StringContent("{}", Encoding.UTF8, "application/json"));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("refreshToken=fixture-refresh-token;", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/api/v1/identity/auth", SetCookieHeaderValue.Parse(cookie).Path.ToString());
        Assert.Equal(TimeSpan.FromDays(7), SetCookieHeaderValue.Parse(cookie).MaxAge);
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
        finally { api.Sender.AuthResultOverride = previous; }
    }

    [Fact]
    public async Task Logout_expires_the_refresh_cookie_at_the_original_path()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, AuthPath + "logout");
        request.Headers.Add("Authorization", "Fixture test");
        using var response = await api.Client.SendAsync(request);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Equal(204, (int)response.StatusCode);
        Assert.StartsWith("refreshToken=;", cookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie);
        Assert.Equal("/api/v1/identity/auth", SetCookieHeaderValue.Parse(cookie).Path.ToString());
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "00000000-0000-0000-0000-000000000018")]
    [InlineData("PUT", "00000000-0000-0000-0000-000000000018/status")]
    public async Task Administrative_routes_reject_non_admin_users(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), UsersPath + path);
        request.Headers.Add("Authorization", "Fixture test");
        request.Headers.Add("X-Role", "Mentee");
        if (method == "PUT") request.Content = new StringContent("""{"isActive":false}""", Encoding.UTF8, "application/json");
        api.Sender.LastRequest = null;
        using var response = await api.Client.SendAsync(request);
        Assert.Equal(403, (int)response.StatusCode);
        Assert.Null(api.Sender.LastRequest);
    }
}
