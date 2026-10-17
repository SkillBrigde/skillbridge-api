using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SkillBridge.BuildingBlocks.Http;
using SkillBridge.BuildingBlocks.Results;
using SkillBridge.BuildingBlocks.Security;
using SkillBridge.Modules.Messaging.Application;
using SkillBridge.Modules.Messaging.Application.DTOs.Requests;
using SkillBridge.Modules.Messaging.Application.DTOs.Responses;

namespace SkillBridge.Modules.Messaging.Endpoints;

public static class MessagingEndpoints
{
    public static void MapMessagingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/messaging").WithTags("Messaging").RequireAuthorization();
        group.MapGet("/conversations", async (int? page, int? pageSize, ICurrentUser user, MessagingService service, CancellationToken ct) =>
        {
            if (user.UserId is not Guid userId) return Results.Unauthorized();
            return (await service.GetConversationsAsync(userId, page ?? 1, pageSize ?? 10, ct)).ToHttpResult();
        }).WithName("GetMessagingConversations");

        group.MapPost("/conversations", async (CreateConversationRequest request, ICurrentUser user, MessagingService service, CancellationToken ct) =>
        {
            if (user.UserId is not Guid userId) return Results.Unauthorized();
            var result = await service.CreateConversationAsync(userId, request, ct);
            if (result.IsFailure) return result.Error.ToProblemDetails();
            return result.Value.Created ? Results.Created($"/api/v1/messaging/conversations/{result.Value.Conversation.Id}/messages", result.Value.Conversation)
                : Results.Ok(result.Value.Conversation);
        }).WithName("CreateMessagingConversation");

        group.MapGet("/conversations/{conversationId:guid}/messages", async (Guid conversationId, string? cursor, int? pageSize,
            ICurrentUser user, MessagingService service, CancellationToken ct) =>
        {
            if (user.UserId is not Guid userId) return Results.Unauthorized();
            return (await service.GetMessagesAsync(userId, conversationId, cursor, pageSize ?? 30, ct)).ToHttpResult();
        }).WithName("GetMessagingMessages");

        group.MapPost("/conversations/{conversationId:guid}/messages", async (Guid conversationId, SendMessageRequest request,
            ICurrentUser user, MessagingService service, CancellationToken ct) =>
        {
            if (user.UserId is not Guid userId) return Results.Unauthorized();
            var result = await service.SendAsync(userId, conversationId, request, ct);
            return result.IsSuccess ? Results.Created($"/api/v1/messaging/conversations/{conversationId}/messages?cursor={result.Value.Sequence + 1}", result.Value)
                : result.Error.ToProblemDetails();
        }).WithName("SendMessagingMessage");

        group.MapPut("/conversations/{conversationId:guid}/messages/{messageId:guid}", async (Guid conversationId, Guid messageId,
            EditMessageRequest request, ICurrentUser user, MessagingService service, CancellationToken ct) =>
        {
            if (user.UserId is not Guid userId) return Results.Unauthorized();
            return (await service.EditAsync(userId, conversationId, messageId, request, ct)).ToHttpResult();
        }).WithName("EditMessagingMessage");

        group.MapDelete("/conversations/{conversationId:guid}/messages/{messageId:guid}", async (Guid conversationId, Guid messageId,
            ICurrentUser user, MessagingService service, CancellationToken ct) =>
        {
            if (user.UserId is not Guid userId) return Results.Unauthorized();
            var result = await service.DeleteAsync(userId, conversationId, messageId, ct);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).WithName("DeleteMessagingMessage");

        group.MapPost("/conversations/{conversationId:guid}/read", async (Guid conversationId, MarkReadRequest request,
            ICurrentUser user, MessagingService service, CancellationToken ct) =>
        {
            if (user.UserId is not Guid userId) return Results.Unauthorized();
            var result = await service.MarkReadAsync(userId, conversationId, request, ct);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemDetails();
        }).WithName("ReadMessagingConversation");

        group.MapGet("/unread-count", async (ICurrentUser user, MessagingService service, CancellationToken ct) =>
        {
            if (user.UserId is not Guid userId) return Results.Unauthorized();
            return Results.Ok(new UnreadCountResponse(await service.GetUnreadCountAsync(userId, ct)));
        }).WithName("GetMessagingUnreadCount");

    }
}
