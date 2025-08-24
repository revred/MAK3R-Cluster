using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace MAK3R.Core.Middleware;

/// <summary>
/// DigitalTwin2 correlation middleware for request tracking and audit trails
/// Ensures every request has a correlation ID for linking events and evidence
/// </summary>
public class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string DataRoomIdHeader = "X-DataRoom-ID";
    private const string SessionIdHeader = "X-Session-ID";

    public CorrelationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract or generate correlation ID
        var correlationId = GetOrCreateCorrelationId(context);
        
        // Extract data room ID from header or JWT claims
        var dataRoomId = GetDataRoomId(context);
        
        // Extract or generate session ID
        var sessionId = GetOrCreateSessionId(context);

        // Add to response headers
        context.Response.Headers[CorrelationIdHeader] = correlationId;
        if (!string.IsNullOrEmpty(dataRoomId))
        {
            context.Response.Headers[DataRoomIdHeader] = dataRoomId;
        }
        context.Response.Headers[SessionIdHeader] = sessionId;

        // Add to Serilog context for structured logging
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("DataRoomId", dataRoomId))
        using (LogContext.PushProperty("SessionId", sessionId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        {
            // Store in HttpContext for downstream access
            context.Items["CorrelationId"] = correlationId;
            context.Items["DataRoomId"] = dataRoomId;
            context.Items["SessionId"] = sessionId;

            await _next(context);
        }
    }

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        // Check request header first
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValue) 
            && !string.IsNullOrWhiteSpace(headerValue))
        {
            var correlationId = headerValue.ToString();
            if (UlidGenerator.IsValid(correlationId))
            {
                return correlationId;
            }
        }

        // Generate new correlation ID
        return DigitalTwinIds.NewCorrelationId();
    }

    private string? GetDataRoomId(HttpContext context)
    {
        // Check request header
        if (context.Request.Headers.TryGetValue(DataRoomIdHeader, out var headerValue) 
            && !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString();
        }

        // Check JWT claims
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var dataRoomClaim = user.FindFirst("dataroom_id");
            if (dataRoomClaim != null && !string.IsNullOrWhiteSpace(dataRoomClaim.Value))
            {
                return dataRoomClaim.Value;
            }
        }

        return null;
    }

    private string GetOrCreateSessionId(HttpContext context)
    {
        // Check request header
        if (context.Request.Headers.TryGetValue(SessionIdHeader, out var headerValue) 
            && !string.IsNullOrWhiteSpace(headerValue))
        {
            var sessionId = headerValue.ToString();
            if (UlidGenerator.IsValid(sessionId))
            {
                return sessionId;
            }
        }

        // Check for existing session in cookies or auth
        var existingSession = context.Request.Cookies["SessionId"];
        if (!string.IsNullOrWhiteSpace(existingSession) && UlidGenerator.IsValid(existingSession))
        {
            return existingSession;
        }

        // Generate new session ID
        var newSessionId = DigitalTwinIds.NewSessionId();
        
        // Store in secure cookie
        context.Response.Cookies.Append("SessionId", newSessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromHours(24)
        });

        return newSessionId;
    }
}

/// <summary>
/// Extension methods for accessing correlation data from HttpContext
/// </summary>
public static class CorrelationExtensions
{
    public static string? GetCorrelationId(this HttpContext context)
    {
        return context.Items["CorrelationId"] as string;
    }

    public static string? GetDataRoomId(this HttpContext context)
    {
        return context.Items["DataRoomId"] as string;
    }

    public static string? GetSessionId(this HttpContext context)
    {
        return context.Items["SessionId"] as string;
    }
}

/// <summary>
/// Service for accessing correlation data in non-HTTP contexts
/// </summary>
public interface ICorrelationContext
{
    string CorrelationId { get; }
    string? DataRoomId { get; }
    string? SessionId { get; }
}

public class CorrelationContext : ICorrelationContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string CorrelationId => 
        _httpContextAccessor.HttpContext?.GetCorrelationId() 
        ?? DigitalTwinIds.NewCorrelationId();

    public string? DataRoomId => 
        _httpContextAccessor.HttpContext?.GetDataRoomId();

    public string? SessionId => 
        _httpContextAccessor.HttpContext?.GetSessionId();
}