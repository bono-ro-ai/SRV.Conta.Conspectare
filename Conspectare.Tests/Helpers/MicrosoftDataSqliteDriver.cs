using NHibernate.Driver;

namespace Conspectare.Tests.Helpers;

public class MicrosoftDataSqliteDriver : ReflectionBasedDriver
{
    public MicrosoftDataSqliteDriver()
        : base(
            "Microsoft.Data.Sqlite",
            "Microsoft.Data.Sqlite",
            "Microsoft.Data.Sqlite.SqliteConnection",
            "Microsoft.Data.Sqlite.SqliteCommand")
    {
    }

    public override bool UseNamedPrefixInSql => true;
    public override bool UseNamedPrefixInParameter => true;
    public override string NamedPrefix => "@";
    public override bool SupportsMultipleOpenReaders => false;
}
