namespace ServiceApp.Core.Models.Queries;

/// <summary>One page of a query plus the total number of matching rows.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
