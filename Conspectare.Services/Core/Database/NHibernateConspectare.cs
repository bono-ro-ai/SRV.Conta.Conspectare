using Conspectare.Infrastructure.NHibernate.Helpers;
using NHibernate;

namespace Conspectare.Services.Core.Database;

/// <summary>
/// Static facade over the application's NHibernate session factory for MariaDB.
/// Configured once at startup and shared for the process lifetime.
/// </summary>
public class NHibernateConspectare
{
    private static INHibernateHelper _internalHelper;

    /// <summary>
    /// Initialises NHibernate with a MySQL/MariaDB connection using the specified mapping assembly.
    /// Must be called once during application startup before any session is opened.
    /// </summary>
    public static void Configure<TMapping>(string connectionString,
        bool showSql = false, bool formatSql = false)
    {
        _internalHelper = new MySqlNHibernateHelper().Configure<TMapping>(connectionString, showSql, formatSql);
    }

    /// <summary>
    /// The underlying NHibernate <see cref="ISessionFactory"/> created during configuration.
    /// </summary>
    public static ISessionFactory SessionFactory => _internalHelper.SessionFactory;

    /// <summary>
    /// Opens a new stateful NHibernate session backed by the configured database.
    /// </summary>
    public static ISession OpenSession() => _internalHelper.OpenSession();

    /// <summary>
    /// Opens a new stateless NHibernate session for bulk read/write operations that bypass the first-level cache.
    /// </summary>
    public static IStatelessSession OpenStatelessSession() => _internalHelper.OpenStatelessSession();

    /// <summary>
    /// Replaces the internal helper with a test double. Used exclusively in integration and unit tests.
    /// </summary>
    public static void ConfigureForTests(INHibernateHelper helper)
    {
        _internalHelper = helper;
    }
}
