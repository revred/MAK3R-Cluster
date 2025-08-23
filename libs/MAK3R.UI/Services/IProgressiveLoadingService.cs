using System.Runtime.CompilerServices;

namespace MAK3R.UI.Services;

/// <summary>
/// Service for progressive loading of data with pagination and streaming
/// </summary>
public interface IProgressiveLoadingService
{
    /// <summary>
    /// Load data progressively with automatic pagination
    /// </summary>
    IAsyncEnumerable<T> LoadProgressivelyAsync<T>(
        Func<int, int, CancellationToken, ValueTask<IEnumerable<T>>> dataLoader,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load data with virtual scrolling support
    /// </summary>
    IAsyncEnumerable<VirtualItem<T>> LoadVirtualizedAsync<T>(
        Func<int, int, CancellationToken, ValueTask<IEnumerable<T>>> dataLoader,
        int totalCount,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Preload data in background for faster access
    /// </summary>
    ValueTask PreloadAsync<T>(
        string cacheKey,
        Func<CancellationToken, ValueTask<T>> dataLoader,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cached data or load if not cached
    /// </summary>
    ValueTask<T?> GetOrLoadAsync<T>(
        string cacheKey,
        Func<CancellationToken, ValueTask<T>> dataLoader,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Virtual item for virtualized loading
/// </summary>
public record VirtualItem<T>(
    int Index,
    T? Data,
    bool IsLoaded,
    bool IsLoading = false
);

/// <summary>
/// Progressive loading options
/// </summary>
public record ProgressiveLoadingOptions(
    int PageSize = 20,
    int PreloadBuffer = 3,
    TimeSpan CacheExpiry = default,
    bool EnablePreloading = true,
    bool EnableVirtualization = false
)
{
    public TimeSpan CacheExpiry { get; init; } = CacheExpiry == default ? TimeSpan.FromMinutes(5) : CacheExpiry;
}