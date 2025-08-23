using Microsoft.Extensions.DependencyInjection;

namespace MAK3R.UI.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add progressive loading services to the DI container
    /// </summary>
    public static IServiceCollection AddProgressiveLoading(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IProgressiveLoadingService, ProgressiveLoadingService>();
        
        return services;
    }
}