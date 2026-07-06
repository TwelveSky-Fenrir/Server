using Fenrir.Tools.LegacyDataImport.Legacy.Readers;
using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Tests;

/// <summary>
///     Parses <c>005_00004.IMG</c> exactly once (raw and patched) for the whole
///     <see cref="MonsterReaderPatchTests" /> class, mirroring <see cref="ItemReaderFixture" />.
/// </summary>
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
