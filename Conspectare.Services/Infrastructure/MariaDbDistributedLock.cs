#nullable enable

using Conspectare.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Conspectare.Services.Infrastructure;

/// <summary>
/// MariaDB/MySQL advisory-lock implementation of <see cref="IDistributedLock"/>.
/// Uses <c>GET_LOCK</c> / <c>RELEASE_LOCK</c> to coordinate across multiple process instances.
/// Lock names are namespaced with the "docpipeline:" prefix to avoid clashes.
/// </summary>
public class MariaDbDistributedLock : IDistributedLock
{
    private readonly string _connectionString;
    private readonly ILogger<MariaDbDistributedLock> _logger;

    public MariaDbDistributedLock(IConfiguration configuration, ILogger<MariaDbDistributedLock> logger)
    {
        _connectionString = configuration.GetConnectionString("ConspectareDb")
            ?? throw new InvalidOperationException("ConnectionStrings:ConspectareDb is required");
        _logger = logger;
    }

    /// <summary>
    /// Attempts to acquire the named advisory lock without waiting (timeout = 0).
    /// Returns an <see cref="IAsyncDisposable"/> handle that releases the lock on disposal,
    /// or <c>null</c> if the lock is already held by another connection.
    /// </summary>
    public async Task<IAsyncDisposable?> TryAcquireAsync(string lockName, CancellationToken ct = default)
    {
        // Namespace the lock to avoid collisions with other subsystems using the same DB.
        var qualifiedName = $"docpipeline:{lockName}";
        var connection = new MySqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(ct);

            // GET_LOCK with timeout=0 returns 1 if acquired, 0 if already locked, NULL on error.
            await using var cmd = new MySqlCommand("SELECT GET_LOCK(@name, 0)", connection);
            cmd.Parameters.AddWithValue("@name", qualifiedName);
            var result = await cmd.ExecuteScalarAsync(ct);

            if (result is not (int or long) || Convert.ToInt32(result) != 1)
            {
                _logger.LogDebug("Lock {LockName} not acquired (held by another instance)", lockName);
                await connection.DisposeAsync();
                return null;
            }

            _logger.LogDebug("Lock {LockName} acquired", lockName);
            return new LockHandle(connection, qualifiedName, lockName, _logger);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Holds an acquired advisory lock for its lifetime.
    /// Issues a no-op keepalive query every 5 minutes to prevent the underlying connection
    /// from being closed by the server's <c>wait_timeout</c>.
    /// Releases the lock and closes the connection on disposal.
    /// </summary>
    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly MySqlConnection _connection;
        private readonly string _qualifiedName;
        private readonly string _lockName;
        private readonly ILogger _logger;
        private readonly Timer _keepAlive;

        // Interlocked flag to guarantee dispose-once semantics.
        private int _disposed;

        public LockHandle(MySqlConnection connection, string qualifiedName, string lockName, ILogger logger)
        {
            _connection = connection;
            _qualifiedName = qualifiedName;
            _lockName = lockName;
            _logger = logger;

            // Fire the keepalive every 5 minutes, starting 5 minutes from now.
            _keepAlive = new Timer(KeepAliveCallback, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Executes a trivial query on the lock connection to reset the server's idle-timeout counter.
        /// Runs on a background ThreadPool thread; failures are logged and swallowed.
        /// </summary>
        private void KeepAliveCallback(object? state)
        {
            if (_disposed == 1) return;

            try
            {
                using var cmd = new MySqlCommand("SELECT 1", _connection);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lock keepalive failed for {LockName}", _lockName);
            }
        }

        public async ValueTask DisposeAsync()
        {
            // Guard against concurrent or double disposal using an atomic exchange.
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

            await _keepAlive.DisposeAsync();

            try
            {
                await using var cmd = new MySqlCommand("SELECT RELEASE_LOCK(@name)", _connection);
                cmd.Parameters.AddWithValue("@name", _qualifiedName);
                await cmd.ExecuteScalarAsync();
                _logger.LogDebug("Lock {LockName} released", _lockName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release lock {LockName}", _lockName);
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }
}
