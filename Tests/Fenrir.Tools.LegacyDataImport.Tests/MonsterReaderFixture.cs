using Fenrir.Tools.LegacyDataImport.Legacy.Readers;
using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Tests;

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
