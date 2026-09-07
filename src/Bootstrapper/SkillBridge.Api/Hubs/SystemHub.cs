using Microsoft.AspNetCore.SignalR;

namespace SkillBridge.Api.Hubs;

public sealed class SystemHub : Hub
{
    public Task Ping() => Clients.Caller.SendAsync("Pong", DateTimeOffset.UtcNow);
}
