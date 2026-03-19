using Conspectare.Api.Middleware;
using Conspectare.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Conspectare.Tests;

public class RateLimitingMiddlewareTests
{
    private static (HttpContext context, TenantContext tenantContext) CreateContext(long tenantId, int rateLimitPerMin)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var tenantContext = new TenantContext
        {
            TenantId = tenantId,
            RateLimitPerMin = rateLimitPerMin
        };

        return (context, tenantContext);
    }

    [Fact]
    public async Task UnderLimit_PassesThrough()
    {
        var nextCalled = false;
        var middleware = new RateLimitingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var (context, tenantContext) = CreateContext(tenantId: 1, rateLimitPerMin: 10);

        await middleware.InvokeAsync(context, tenantContext);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
    }

    [Fact]
    public async Task AtLimit_Returns429WithRetryAfter()
    {
        var middleware = new RateLimitingMiddleware(_ => Task.CompletedTask);
        const int limit = 3;
        const long tenantId = 100;

        // Exhaust the limit
        for (var i = 0; i < limit; i++)
        {
            var (ctx, tc) = CreateContext(tenantId, limit);
            await middleware.InvokeAsync(ctx, tc);
        }

        // Next request should be rejected
        var (context, tenantContext) = CreateContext(tenantId, limit);
        await middleware.InvokeAsync(context, tenantContext);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("60", context.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task DifferentTenants_SeparateBuckets()
    {
        var middleware = new RateLimitingMiddleware(_ => Task.CompletedTask);
        const int limit = 2;

        // Exhaust limit for tenant 1
        for (var i = 0; i < limit; i++)
        {
            var (ctx, tc) = CreateContext(tenantId: 1, limit);
            await middleware.InvokeAsync(ctx, tc);
        }

        // Tenant 2 should still be allowed
        var nextCalled = false;
        var middleware2 = new RateLimitingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var (context, tenantContext) = CreateContext(tenantId: 2, limit);
        await middleware2.InvokeAsync(context, tenantContext);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_SkipsRateLimiting()
    {
        var nextCalled = false;
        var middleware = new RateLimitingMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var (context, tenantContext) = CreateContext(tenantId: 0, rateLimitPerMin: 10);

        await middleware.InvokeAsync(context, tenantContext);

        Assert.True(nextCalled);
    }
}
