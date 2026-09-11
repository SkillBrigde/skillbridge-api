namespace SkillBridge.BuildingBlocks.Pagination;

public record PaginationParams
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    private int _pageSize = DefaultPageSize;
    private int _pageNumber = 1;

    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value > MaxPageSize ? MaxPageSize : (value < 1 ? DefaultPageSize : value);
    }
}
