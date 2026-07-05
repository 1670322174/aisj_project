namespace InteriorDesignWeb.Application.Common;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long Total { get; set; }

    public bool HasNext => Page * PageSize < Total;

    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, long total)
    {
        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }
}
