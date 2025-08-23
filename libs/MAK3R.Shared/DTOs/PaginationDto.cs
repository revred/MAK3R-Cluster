namespace MAK3R.Shared.DTOs;

/// <summary>
/// Generic paginated response wrapper
/// </summary>
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext => Page < TotalPages - 1;
    public bool HasPrevious => Page > 0;
}

/// <summary>
/// Pagination request parameters
/// </summary>
public class PageRequest
{
    public int Page { get; set; } = 0;
    public int Size { get; set; } = 20;
    public string? Sort { get; set; }
    public string? Filter { get; set; }
    public string? Search { get; set; }
    
    public PageRequest()
    {
    }
    
    public PageRequest(int page, int size)
    {
        Page = page;
        Size = size;
    }
}

/// <summary>
/// Streaming response for progressive loading
/// </summary>
public class StreamingResponse<T>
{
    public T Data { get; set; } = default!;
    public int Index { get; set; }
    public bool IsLast { get; set; }
    public string? NextToken { get; set; }
}