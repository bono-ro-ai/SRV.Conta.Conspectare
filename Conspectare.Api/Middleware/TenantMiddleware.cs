using Conspectare.Infrastructure.Extensions;
using Conspectare.Services.Interfaces;
using ISession = NHibernate.ISession;

namespace Conspectare.Api.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ISession session)
    {
        if (tenantContext.TenantId > 0)
        {
            session.EnableTenantFilter(tenantContext.TenantId);
        }

        await _next(context);
    }
}
