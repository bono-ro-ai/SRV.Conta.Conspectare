#nullable enable

using Conspectare.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Conspectare.Services.Infrastructure;

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

    public async Task<IAsyncDisposable?> TryAcquireAsync(string lockName, CancellationToken ct = default)
    {
        var qualifiedName = $"docpipeline:{lockName}";
        var connection = new MySqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct);
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

    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly MySqlConnection _connection;
        private readonly string _qualifiedName;
        private readonly string _lockName;
        private readonly ILogger _logger;
        private readonly Timer _keepAlive;
        private int _disposed;

        public LockHandle(MySqlConnection connection, string qualifiedName, string lockName, ILogger logger)
        {
            _connection = connection;
            _qualifiedName = qualifiedName;
            _lockName = lockName;
            _logger = logger;
            _keepAlive = new Timer(KeepAliveCallback, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

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
