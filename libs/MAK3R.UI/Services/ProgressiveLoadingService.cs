using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace MAK3R.UI.Services;

public class ProgressiveLoadingService : IProgressiveLoadingService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProgressiveLoadingService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _loadingSemaphores = new();

    public ProgressiveLoadingService(
        IMemoryCache cache,
        ILogger<ProgressiveLoadingService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async IAsyncEnumerable<T> LoadProgressivelyAsync<T>(
        Func<int, int, CancellationToken, ValueTask<IEnumerable<T>>> dataLoader,
        int pageSize = 20,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var page = 0;
        var hasMore = true;

        while (hasMore && !cancellationToken.IsCancellationRequested)
        {
            IEnumerable<T> items;
            try
            {
                _logger.LogDebug("Loading page {Page} with size {PageSize}", page, pageSize);
                
                items = await dataLoader(page, pageSize, cancellationToken);
                var itemsList = items.ToList();
                
                hasMore = itemsList.Count == pageSize;
                items = itemsList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading page {Page}", page);
                hasMore = false;
                continue;
            }
            
            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                    yield break;
                    
                yield return item;
            }
            
            page++;
            
            // Small delay to prevent overwhelming the API
            if (hasMore)
                await Task.Delay(10, cancellationToken);
        }
    }

    public async IAsyncEnumerable<VirtualItem<T>> LoadVirtualizedAsync<T>(
        Func<int, int, CancellationToken, ValueTask<IEnumerable<T>>> dataLoader,
        int totalCount,
        int pageSize = 50,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var loadedPages = new ConcurrentDictionary<int, List<T>>();
        var loadingPages = new ConcurrentDictionary<int, Task<List<T>>>();

        for (int i = 0; i < totalCount; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var pageIndex = i / pageSize;
            var itemIndex = i % pageSize;

            // Check if page is already loaded
            if (loadedPages.TryGetValue(pageIndex, out var loadedPage))
            {
                var item = itemIndex < loadedPage.Count ? loadedPage[itemIndex] : default(T);
                yield return new VirtualItem<T>(i, item, item != null);
                continue;
            }

            // Check if page is currently loading
            if (loadingPages.TryGetValue(pageIndex, out var loadingTask))
            {
                yield return new VirtualItem<T>(i, default(T), false, true);
                
                // Wait for the page to load in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await loadingTask;
                        loadedPages.TryAdd(pageIndex, result);
                        loadingPages.TryRemove(pageIndex, out _);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading virtual page {PageIndex}", pageIndex);
                        loadingPages.TryRemove(pageIndex, out _);
                    }
                }, cancellationToken);
                continue;
            }

            // Start loading the page
            var loadTask = Task.Run(async () =>
            {
                try
                {
                    var items = await dataLoader(pageIndex, pageSize, cancellationToken);
                    return items.ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading page {PageIndex}", pageIndex);
                    return new List<T>();
                }
            }, cancellationToken);

            loadingPages.TryAdd(pageIndex, loadTask);
            yield return new VirtualItem<T>(i, default(T), false, true);
        }
    }

    public async ValueTask PreloadAsync<T>(
        string cacheKey,
        Func<CancellationToken, ValueTask<T>> dataLoader,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(cacheKey, out _))
        {
            _logger.LogDebug("Data already cached for key {CacheKey}", cacheKey);
            return;
        }

        var semaphore = _loadingSemaphores.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        
        if (!await semaphore.WaitAsync(100, cancellationToken))
        {
            _logger.LogDebug("Another thread is already loading data for key {CacheKey}", cacheKey);
            return;
        }

        try
        {
            // Double-check after acquiring the lock
            if (_cache.TryGetValue(cacheKey, out _))
                return;

            _logger.LogDebug("Preloading data for key {CacheKey}", cacheKey);
            
            var data = await dataLoader(cancellationToken);
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(5),
                Priority = CacheItemPriority.Normal
            };
            
            _cache.Set(cacheKey, data, options);
            _logger.LogDebug("Data preloaded and cached for key {CacheKey}", cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preloading data for key {CacheKey}", cacheKey);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async ValueTask<T?> GetOrLoadAsync<T>(
        string cacheKey,
        Func<CancellationToken, ValueTask<T>> dataLoader,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out T? cachedData))
        {
            _logger.LogDebug("Cache hit for key {CacheKey}", cacheKey);
            return cachedData;
        }

        var semaphore = _loadingSemaphores.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        
        await semaphore.WaitAsync(cancellationToken);
        
        try
        {
            // Double-check after acquiring the lock
            if (_cache.TryGetValue(cacheKey, out cachedData))
                return cachedData;

            _logger.LogDebug("Cache miss, loading data for key {CacheKey}", cacheKey);
            
            var data = await dataLoader(cancellationToken);
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(5),
                Priority = CacheItemPriority.Normal
            };
            
            _cache.Set(cacheKey, data, options);
            return data;
        }
        finally
        {
            semaphore.Release();
        }
    }
}