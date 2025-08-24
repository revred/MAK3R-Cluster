using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MAK3R.Core.Middleware;

/// <summary>
/// DigitalTwin2 middleware registration extensions
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add DigitalTwin2 correlation services
    /// </summary>
    public static IServiceCollection AddDigitalTwinCorrelation(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationContext, CorrelationContext>();
        
        return services;
    }

    /// <summary>
    /// Use DigitalTwin2 correlation middleware
    /// Should be added early in the pipeline for comprehensive request tracking
    /// </summary>
    public static IApplicationBuilder UseDigitalTwinCorrelation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationMiddleware>();
    }
}