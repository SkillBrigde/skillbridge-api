using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Identity.Domain;
using SkillBridge.Modules.Identity.Infrastructure.Data;
using SkillBridge.Modules.Messaging.Domain;
using SkillBridge.Modules.Messaging.Infrastructure;

namespace SkillBridge.Checks;

internal static class MessagingChecks
{
    public static async Task RunAsync(HttpClient client, IServiceProvider services)
    {
        var a = await SeedAsync(services, "messaging-a@checks.test", "Mentor");
        var b = await SeedAsync(services, "messaging-b@checks.test", "Mentee");
        var c = await SeedAsync(services, "messaging-c@checks.test", "Mentee");
        const string route = "/api/v1/messaging/conversations";
        using var anonymous = await DatabaseChecks.SendAsync(client, HttpMethod.Get, route, 401);
        using var self = await DatabaseChecks.SendAsync(client, HttpMethod.Post, route, 400, new { targetUserId = a.Id }, a.Token);
        using var missing = await DatabaseChecks.SendAsync(client, HttpMethod.Post, route, 404, new { targetUserId = Guid.NewGuid() }, a.Token);
        var pair = await Task.WhenAll(
            DatabaseChecks.SendAsync(client, HttpMethod.Post, route, null, new { targetUserId = b.Id }, a.Token),
            DatabaseChecks.SendAsync(client, HttpMethod.Post, route, null, new { targetUserId = a.Id }, b.Token));
        Guid conversation;
        try
        {
            Program.Check(pair.Count(x => (int)x.StatusCode == 201) == 1 && pair.Count(x => (int)x.StatusCode == 200) == 1,
                "Concurrent pair creation must create exactly one conversation.");
            conversation = (await DatabaseChecks.JsonAsync(pair[0])).GetProperty("id").GetGuid();
            Program.Check((await DatabaseChecks.JsonAsync(pair[1])).GetProperty("id").GetGuid() == conversation,
                "Reversed participant pair created another conversation.");
        }
        finally { foreach (var response in pair) response.Dispose(); }
        var messages = $"{route}/{conversation}/messages";
        using var outsider = await DatabaseChecks.SendAsync(client, HttpMethod.Get, messages, 404, bearer: c.Token);
        using var outsiderSend = await DatabaseChecks.SendAsync(client, HttpMethod.Post, messages, 404, new { content = "forbidden" }, c.Token);
        using var invalidText = await DatabaseChecks.SendAsync(client, HttpMethod.Post, messages, 400, new { content = " " }, a.Token);
        using var invalidType = await DatabaseChecks.SendAsync(client, HttpMethod.Post, messages, 400, new { content = "system", type = "System" }, a.Token);
        using var first = await DatabaseChecks.SendAsync(client, HttpMethod.Post, messages, 201, new { content = "First" }, a.Token);
        var firstId = (await DatabaseChecks.JsonAsync(first)).GetProperty("id").GetGuid();
        using var second = await DatabaseChecks.SendAsync(client, HttpMethod.Post, messages, 201, new { content = "Second" }, a.Token);
        var secondJson = await DatabaseChecks.JsonAsync(second);
        var secondId = secondJson.GetProperty("id").GetGuid();
        Program.Check(secondJson.GetProperty("sequence").GetInt64() == 2, "Message sequence did not advance.");
        await CountAsync(client, b.Token, 2);
        await CountAsync(client, a.Token, 0);
        using var page = await DatabaseChecks.SendAsync(client, HttpMethod.Get, messages + "?pageSize=1", 200, bearer: b.Token);
        var pageJson = await DatabaseChecks.JsonAsync(page);
        var cursor = pageJson.GetProperty("nextCursor").GetString();
        Program.Check(pageJson.GetProperty("items")[0].GetProperty("id").GetGuid() == secondId && cursor is not null, "Cursor page ordering failed.");
        using var next = await DatabaseChecks.SendAsync(client, HttpMethod.Get, messages + "?pageSize=1&cursor=" + cursor, 200, bearer: b.Token);
        Program.Check((await DatabaseChecks.JsonAsync(next)).GetProperty("items")[0].GetProperty("id").GetGuid() == firstId, "Cursor paging duplicated/skipped history.");
        using var badCursor = await DatabaseChecks.SendAsync(client, HttpMethod.Get, messages + "?cursor=bad", 400, bearer: a.Token);
        using var badPage = await DatabaseChecks.SendAsync(client, HttpMethod.Get, route + "?pageSize=101", 400, bearer: a.Token);
        using var wrongEdit = await DatabaseChecks.SendAsync(client, HttpMethod.Put, $"{messages}/{firstId}", 403, new { content = "Not yours" }, b.Token);
        using var wrongDelete = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, $"{messages}/{firstId}", 403, bearer: b.Token);
        using var outsiderEdit = await DatabaseChecks.SendAsync(client, HttpMethod.Put, $"{messages}/{firstId}", 404, new { content = "Not yours" }, c.Token);
        using var outsiderDelete = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, $"{messages}/{firstId}", 404, bearer: c.Token);
        using var edited = await DatabaseChecks.SendAsync(client, HttpMethod.Put, $"{messages}/{secondId}", 200, new { content = "Updated" }, a.Token);
        using var list = await DatabaseChecks.SendAsync(client, HttpMethod.Get, route, 200, bearer: b.Token);
        var listJson = await DatabaseChecks.JsonAsync(list);
        Program.Check(listJson.GetProperty("items")[0].GetProperty("lastMessagePreview").GetString() == "Updated" &&
            listJson.GetProperty("items")[0].GetProperty("unreadCount").GetInt32() == 2, "Conversation summary/read count is inconsistent.");
        var read = $"{route}/{conversation}/read";
        using var readDenied = await DatabaseChecks.SendAsync(client, HttpMethod.Post, read, 404, new { messageId = secondId }, c.Token);
        using var marked = await DatabaseChecks.SendAsync(client, HttpMethod.Post, read, 204, new { messageId = secondId }, b.Token);
        using var regressed = await DatabaseChecks.SendAsync(client, HttpMethod.Post, read, 204, new { messageId = firstId }, b.Token);
        await CountAsync(client, b.Token, 0);

        using var another = await DatabaseChecks.SendAsync(client, HttpMethod.Post, route, 201, new { targetUserId = c.Id }, a.Token);
        var anotherId = (await DatabaseChecks.JsonAsync(another)).GetProperty("id").GetGuid();
        using var crossedRead = await DatabaseChecks.SendAsync(client, HttpMethod.Post, $"{route}/{anotherId}/read", 404,
            new { messageId = secondId }, a.Token);
        using var crossedEdit = await DatabaseChecks.SendAsync(client, HttpMethod.Put, $"{route}/{anotherId}/messages/{secondId}", 404,
            new { content = "Wrong conversation" }, a.Token);

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            DatabaseChecks.SendAsync(client, HttpMethod.Post, messages, 201, new { content = "Concurrent " + i }, a.Token)));
        foreach (var response in concurrent) response.Dispose();
        await CountAsync(client, b.Token, 6);
        using var removed = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, $"{messages}/{secondId}", 204, bearer: a.Token);
        using var removedAgain = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, $"{messages}/{secondId}", 204, bearer: a.Token);
        using var editRemoved = await DatabaseChecks.SendAsync(client, HttpMethod.Put, $"{messages}/{secondId}", 409, new { content = "Restore" }, a.Token);
        using var history = await DatabaseChecks.SendAsync(client, HttpMethod.Get, messages, 200, bearer: b.Token);
        var tombstone = (await DatabaseChecks.JsonAsync(history)).GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == secondId);
        Program.Check(tombstone.GetProperty("isDeleted").GetBoolean() && tombstone.GetProperty("content").GetString() == "", "Deleted message leaked original content.");
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            Program.Check(await db.OutboxMessages.CountAsync() == 8 &&
                await db.Messages.Where(x => x.ConversationId == conversation).Select(x => x.Sequence).Distinct().CountAsync() == 8,
                "Concurrent messages/outbox entries were lost or duplicated.");
            var users = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = await users.Users.SingleAsync(x => x.Id == b.Id);
            user.SetStatus(false);
            await users.SaveChangesAsync();
        }
        using var unavailable = await DatabaseChecks.SendAsync(client, HttpMethod.Post, messages, 409, new { content = "Disabled recipient" }, a.Token);
        using var disabled = await DatabaseChecks.SendAsync(client, HttpMethod.Get, route, 401, bearer: b.Token);
        Console.WriteLine("PASS: Messaging REST membership, ownership, cursor/read state, concurrent writes, outbox and disabled users.");
    }

    internal static async Task<(Guid Id, string Token)> SeedAsync(IServiceProvider services, string email, string role)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = User.Create(email, scope.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword("Password123!"), "Messaging checks", role);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>().GenerateAccessToken(
            user.Id, user.Email, user.FullName, [role], user.SecurityStamp));
    }

    private static async Task CountAsync(HttpClient client, string token, int expected)
    {
        using var response = await DatabaseChecks.SendAsync(client, HttpMethod.Get, "/api/v1/messaging/unread-count", 200, bearer: token);
        Program.Check((await DatabaseChecks.JsonAsync(response)).GetProperty("count").GetInt32() == expected, "Unread count is incorrect.");
    }
}
