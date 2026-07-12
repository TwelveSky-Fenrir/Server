using Fenrir.Tools.DbMigrator.Legacy.Readers;
using Fenrir.Tools.DbMigrator.Legacy.Records;

namespace Fenrir.Tools.DbMigrator.Tests;

public sealed class MonsterReaderFixture
{
    public MonsterReaderFixture()
    {
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "LegacyData");
        Raw = MonsterReader.ReadAllRaw(dataDirectory);
        Patched = MonsterReader.ReadAll(dataDirectory);
    }

    internal IReadOnlyList<MonsterRecord> Raw { get; }
    internal IReadOnlyList<MonsterRecord> Patched { get; }
}
