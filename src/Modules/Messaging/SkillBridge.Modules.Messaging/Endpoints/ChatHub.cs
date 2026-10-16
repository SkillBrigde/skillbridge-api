using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.Modules.Messaging.Application;
using SkillBridge.Modules.Messaging.Infrastructure;

namespace SkillBridge.Modules.Messaging.Endpoints;

[Authorize]
public sealed class ChatHub(MessagingService service, IMessagingUserDirectory users, ChatConnections connections, TimeProvider clock) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = await ValidUserAsync();
        if (userId is null)
        {
            Context.Abort();
            return;
        }
        connections.Add(Context.ConnectionId, userId.Value, Context.User!.FindFirst("security_stamp")!.Value,
            long.Parse(Context.User.FindFirst("exp")!.Value, CultureInfo.InvariantCulture));
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task<ChatResult<MessageDto>> SendMessage(Guid conversationId, string? content)
    {
        var userId = await ValidUserAsync();
        return userId is null ? ChatResult<MessageDto>.Unauthorized() : ChatResult<MessageDto>.From(
            await service.SendAsync(userId.Value, conversationId, new(content), Context.ConnectionAborted));
    }

    public async Task<ChatResult<bool>> MarkAsRead(Guid conversationId, Guid messageId)
    {
        var userId = await ValidUserAsync();
        return userId is null ? ChatResult<bool>.Unauthorized() : ChatResult<bool>.From(
            await service.MarkReadAsync(userId.Value, conversationId, new(messageId), Context.ConnectionAborted));
    }

    public Task<ChatResult<bool>> StartTyping(Guid conversationId) => TypingAsync(conversationId, true);
    public Task<ChatResult<bool>> StopTyping(Guid conversationId) => TypingAsync(conversationId, false);

    private async Task<ChatResult<bool>> TypingAsync(Guid conversationId, bool typing)
    {
        var userId = await ValidUserAsync();
        return userId is null ? ChatResult<bool>.Unauthorized() : ChatResult<bool>.From(
            await service.TypingAsync(userId.Value, conversationId, typing, Context.ConnectionAborted));
    }

    private async Task<Guid?> ValidUserAsync()
    {
        var principal = Context.User;
        if (!Guid.TryParse(principal?.FindFirst("sub")?.Value, out var userId) || userId == Guid.Empty ||
            !long.TryParse(principal.FindFirst("exp")?.Value, out var expires) || expires <= clock.GetUtcNow().ToUnixTimeSeconds() ||
            principal.FindFirst("security_stamp")?.Value is not { Length: > 0 } stamp ||
            !await users.IsSessionValidAsync(userId, stamp, Context.ConnectionAborted))
        {
            connections.Remove(Context.ConnectionId);
            return null;
        }
        return userId;
    }
}

public sealed record ChatError(string Code, string Description);
public sealed record ChatResult<T>(bool IsSuccess, T? Value, ChatError? Error)
{
    public static ChatResult<T> From(Result<T> result) => result.IsSuccess
        ? new(true, result.Value, null) : new(false, default, new(result.Error.Code, result.Error.Description));
    public static ChatResult<T> Unauthorized() => new(false, default, new("Messaging.SessionExpired", "Authenticate again."));
}
