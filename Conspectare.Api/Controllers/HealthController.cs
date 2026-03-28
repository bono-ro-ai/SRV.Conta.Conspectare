using Conspectare.Api.DTOs;
using Conspectare.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using NHibernate;
using ISession = NHibernate.ISession;

namespace Conspectare.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly ISessionFactory _sessionFactory;
    private readonly IStorageService _storageService;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ISessionFactory sessionFactory,
        IStorageService storageService,
        ILogger<HealthController> logger)
    {
        _sessionFactory = sessionFactory;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Probes the database and S3 storage to assess service health.
    /// Returns HTTP 200 when both dependencies are reachable, or HTTP 503 when either fails.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dbStatus = "ok";
        var s3Status = "ok";

        try
        {
            using var session = _sessionFactory.OpenSession();
            await session.CreateSQLQuery("SELECT 1").UniqueResultAsync<object>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check: database probe failed");
            dbStatus = "error";
        }

        try
        {
            // ExistsAsync with a sentinel key is sufficient to verify S3 connectivity.
            await _storageService.ExistsAsync("health-probe", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check: S3 probe failed");
            s3Status = "error";
        }

        var isHealthy = dbStatus == "ok" && s3Status == "ok";
        var status = isHealthy ? "healthy" : "degraded";
        var statusCode = isHealthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return new ObjectResult(new HealthResponse(status, dbStatus, s3Status))
        {
            StatusCode = statusCode
        };
    }
}
