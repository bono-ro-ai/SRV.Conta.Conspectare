using Conspectare.Infrastructure.Extensions;
using Conspectare.Services.Interfaces;
using ISession = NHibernate.ISession;

namespace Conspectare.Api.Middleware;

/// <summary>
/// Activates the NHibernate tenant filter on the current session so that all subsequent
/// queries within the request are automatically scoped to the authenticated tenant.
/// Must run after authentication and authorization middleware.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Applies the tenant filter when a valid tenant ID is present on the context,
    /// then forwards the request to the next middleware in the pipeline.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ISession session)
    {
        // Only enable the filter for authenticated, tenant-scoped requests;
        // anonymous or admin-only requests may have TenantId == 0.
        if (tenantContext.TenantId > 0)
        {
            session.EnableTenantFilter(tenantContext.TenantId);
        }

        await _next(context);
    }
}
