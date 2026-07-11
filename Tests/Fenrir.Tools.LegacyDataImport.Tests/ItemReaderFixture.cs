using Fenrir.Tools.LegacyDataImport.Legacy.Readers;
using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Tests;

public sealed class ItemReaderFixture
{
    public ItemReaderFixture()
    {
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "LegacyData");
        Raw = ItemReader.ReadAllRaw(dataDirectory);
        Patched = ItemReader.ReadAll(dataDirectory);
    }

    internal IReadOnlyList<ItemRecord> Raw { get; }
    internal IReadOnlyList<ItemRecord> Patched { get; }
}
