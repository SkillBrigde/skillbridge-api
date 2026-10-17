using Microsoft.AspNetCore.Mvc;

namespace SkillBridge.Modules.Identity.Application.DTOs.Requests;

public sealed class GetUsersRequest
{
    [FromQuery(Name = "page")]
    public int? Page { get; init; }

    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; init; }

    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    [FromQuery(Name = "role")]
    public string? Role { get; init; }

    [FromQuery(Name = "isActive")]
    public bool? IsActive { get; init; }
}
