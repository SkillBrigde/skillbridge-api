namespace SkillBridge.Modules.Identity.Application.Users.DTOs.Responses;

public sealed record UsersResponse(
    IReadOnlyList<UserSummaryResponse> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);
