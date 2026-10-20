using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SkillBridge.Modules.Messaging.Application;
using SkillBridge.Modules.Messaging.Endpoints;

namespace SkillBridge.Modules.Messaging.Infrastructure;

internal sealed class SignalRMessageNotifier(IHubContext<ChatHub> hub, ChatConnections connections,
    IMessagingUserDirectory users) : IMessageNotifier
{
    public async Task PublishAsync(IReadOnlyCollection<Guid> recipients, string eventName, object?[] arguments, CancellationToken cancellationToken)
    {
        var authorized = await connections.AuthorizedAsync(recipients, users, cancellationToken);
        if (authorized.Count > 0)
            await hub.Clients.Clients(authorized).SendCoreAsync(eventName, arguments, cancellationToken);
    }
}

// shortcut: registry chỉ dùng trong một instance; cần thiết kế delivery/backplane trước khi chạy nhiều replica.
public sealed class ChatConnections(TimeProvider clock)
{
    private sealed record Session(Guid UserId, string Stamp, long Expires);
    private readonly ConcurrentDictionary<string, Session> _connections = new();
    public void Add(string connectionId, Guid userId, string stamp, long expires) => _connections[connectionId] = new(userId, stamp, expires);
    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    public async Task<IReadOnlyList<string>> AuthorizedAsync(IReadOnlyCollection<Guid> userIds, IMessagingUserDirectory users, CancellationToken ct)
    {
        List<string> authorized = [];
        foreach (var (connectionId, session) in _connections)
        {
            if (!userIds.Contains(session.UserId)) continue;
            if (session.Expires > clock.GetUtcNow().ToUnixTimeSeconds() && await users.IsSessionValidAsync(session.UserId, session.Stamp, ct))
                authorized.Add(connectionId);
            else
                Remove(connectionId);
        }
        return authorized;
    }
}
