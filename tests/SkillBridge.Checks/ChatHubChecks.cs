using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkillBridge.Modules.Identity.Infrastructure.Data;

namespace SkillBridge.Checks;

internal static class ChatHubChecks
{
    public static async Task RunAsync(HttpClient client, IServiceProvider services)
    {
        var a = await MessagingChecks.SeedAsync(services, "hub-a@checks.test", "Mentor");
        var b = await MessagingChecks.SeedAsync(services, "hub-b@checks.test", "Mentee");
        var c = await MessagingChecks.SeedAsync(services, "hub-c@checks.test", "Mentee");
        using var anonymous = await client.PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null);
        Program.Check((int)anonymous.StatusCode == 401, "Hub must require authentication.");
        using var unrelated = await client.GetAsync("/api/v1/messaging/conversations?access_token=" + Uri.EscapeDataString(a.Token));
        Program.Check((int)unrelated.StatusCode == 401, "Query tokens must not authenticate unrelated REST routes.");
        using var pair = await DatabaseChecks.SendAsync(client, HttpMethod.Post, "/api/v1/messaging/conversations", 201,
            new { targetUserId = b.Id }, a.Token);
        var conversation = (await DatabaseChecks.JsonAsync(pair)).GetProperty("id").GetGuid();
        await using var sender = await Socket.ConnectAsync(client.BaseAddress!, a.Token, false);
        await using var recipient = await Socket.ConnectAsync(client.BaseAddress!, b.Token, true);
        await using var stranger = await Socket.ConnectAsync(client.BaseAddress!, c.Token, false);
        var sent = await sender.InvokeAsync("SendMessage", conversation, "Hello realtime");
        Program.Check(sent.GetProperty("isSuccess").GetBoolean(), "Hub send failed.");
        var messageId = sent.GetProperty("value").GetProperty("id").GetGuid();
        var received = await recipient.EventAsync("ReceiveMessage");
        Program.Check(received.GetProperty("arguments")[0].GetProperty("id").GetGuid() == messageId &&
            received.GetProperty("arguments")[0].GetProperty("senderId").GetGuid() == a.Id, "Hub delivered a different sender/message.");
        var marked = await recipient.InvokeAsync("MarkAsRead", conversation, messageId);
        Program.Check(marked.GetProperty("isSuccess").GetBoolean(), "Hub mark-read failed.");
        using var unread = await DatabaseChecks.SendAsync(client, HttpMethod.Get, "/api/v1/messaging/unread-count", 200, bearer: b.Token);
        Program.Check((await DatabaseChecks.JsonAsync(unread)).GetProperty("count").GetInt32() == 0, "Hub read did not persist.");
        var typing = await sender.InvokeAsync("StartTyping", conversation);
        Program.Check(typing.GetProperty("isSuccess").GetBoolean(), "StartTyping failed.");
        var typed = await recipient.EventAsync("UserTyping");
        Program.Check(typed.GetProperty("arguments")[0].GetGuid() == conversation, "Typing event reached wrong conversation.");
        await sender.InvokeAsync("StopTyping", conversation);
        await recipient.EventAsync("UserStoppedTyping");
        var messagePath = $"/api/v1/messaging/conversations/{conversation}/messages/{messageId}";
        using var edited = await DatabaseChecks.SendAsync(client, HttpMethod.Put, messagePath, 200, new { content = "Edited live" }, a.Token);
        var editEvent = await recipient.EventAsync("MessageEdited");
        Program.Check(editEvent.GetProperty("arguments")[1].GetString() == "Edited live", "REST edit did not notify Hub clients.");
        using var removed = await DatabaseChecks.SendAsync(client, HttpMethod.Delete, messagePath, 204, bearer: a.Token);
        await recipient.EventAsync("MessageDeleted");
        var denied = await stranger.InvokeAsync("SendMessage", conversation, "Unauthorized");
        Program.Check(!denied.GetProperty("isSuccess").GetBoolean() && stranger.Events.Count == 0,
            "Outsider sent or received a private conversation message.");

        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == b.Id);
            user.ChangePassword(user.PasswordHash);
            await db.SaveChangesAsync();
        }
        var afterRevocation = await sender.InvokeAsync("SendMessage", conversation, "After stamp rotation");
        Program.Check(afterRevocation.GetProperty("isSuccess").GetBoolean(), "Active sender should still send to the account.");
        var revoked = await recipient.InvokeAsync("MarkAsRead", conversation, messageId);
        Program.Check(!revoked.GetProperty("isSuccess").GetBoolean() &&
            revoked.GetProperty("error").GetProperty("code").GetString() == "Messaging.SessionExpired" && recipient.Events.Count == 0,
            "Revoked connection invoked methods or kept receiving private messages.");
        using var staleNegotiate = await DatabaseChecks.SendAsync(client, HttpMethod.Post,
            "/hubs/chat/negotiate?negotiateVersion=1", 401, bearer: b.Token);
        Console.WriteLine("PASS: native SignalR WebSockets, query/header auth, send/read/typing/edit/delete, membership and revoked-session delivery.");
    }

    private sealed class Socket : IAsyncDisposable
    {
        private readonly ClientWebSocket _socket = new();
        private readonly Queue<JsonElement> _frames = new();
        private int _id;
        public List<JsonElement> Events { get; } = [];

        public static async Task<Socket> ConnectAsync(Uri origin, string token, bool queryToken)
        {
            var connection = new Socket();
            if (!queryToken) connection._socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
            var uri = new UriBuilder(origin)
            {
                Scheme = "ws",
                Path = "/hubs/chat",
                Query = queryToken ? "access_token=" + Uri.EscapeDataString(token) : ""
            }.Uri;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await connection._socket.ConnectAsync(uri, timeout.Token);
            await connection.SendAsync(new { protocol = "json", version = 1 });
            var handshake = await connection.NextAsync();
            Program.Check(!handshake.TryGetProperty("error", out _), "SignalR handshake failed.");
            return connection;
        }

        public async Task<JsonElement> InvokeAsync(string method, params object[] arguments)
        {
            var id = (++_id).ToString(System.Globalization.CultureInfo.InvariantCulture);
            await SendAsync(new { type = 1, invocationId = id, target = method, arguments });
            while (true)
            {
                var frame = await NextAsync();
                var type = frame.GetProperty("type").GetInt32();
                if (type == 1) { Events.Add(frame); continue; }
                if (type == 3 && frame.GetProperty("invocationId").GetString() == id)
                {
                    Program.Check(!frame.TryGetProperty("error", out _), "Hub invocation returned a protocol error.");
                    return frame.GetProperty("result").Clone();
                }
                Program.Check(type == 6, "Unexpected SignalR frame during invocation.");
            }
        }

        public async Task<JsonElement> EventAsync(string method)
        {
            var index = Events.FindIndex(x => x.GetProperty("target").GetString() == method);
            if (index >= 0) { var found = Events[index]; Events.RemoveAt(index); return found; }
            while (true)
            {
                var frame = await NextAsync();
                if (frame.GetProperty("type").GetInt32() == 6) continue;
                Program.Check(frame.GetProperty("type").GetInt32() == 1, "Expected a SignalR event.");
                if (frame.GetProperty("target").GetString() == method) return frame;
                Events.Add(frame);
            }
        }

        private async Task SendAsync(object value)
        {
            var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value) + "\u001e");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, timeout.Token);
        }

        private async Task<JsonElement> NextAsync()
        {
            if (_frames.Count > 0) return _frames.Dequeue();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var message = new MemoryStream();
            byte[] buffer = new byte[8192];
            WebSocketReceiveResult result;
            do
            {
                result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                Program.Check(result.MessageType == WebSocketMessageType.Text, "WebSocket closed before expected frame.");
                message.Write(buffer, 0, result.Count);
                Program.Check(message.Length <= 65536, "Unexpected oversized test frame.");
            } while (!result.EndOfMessage);
            foreach (var json in Encoding.UTF8.GetString(message.ToArray()).Split('\u001e', StringSplitOptions.RemoveEmptyEntries))
            {
                using var document = JsonDocument.Parse(json);
                _frames.Enqueue(document.RootElement.Clone());
            }
            return _frames.Dequeue();
        }

        public ValueTask DisposeAsync()
        {
            _socket.Abort();
            _socket.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
