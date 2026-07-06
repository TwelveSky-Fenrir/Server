using Fenrir.Tools.LegacyDataImport.Legacy.Readers;
using Fenrir.Tools.LegacyDataImport.Legacy.Records;

namespace Fenrir.Tools.LegacyDataImport.Tests;

/// <summary>
///     Parses <c>005_00002.IMG</c> exactly once (raw and patched) for the whole <see cref="ItemReaderPatchTests" />
///     class -- <see cref="ItemReader.ReadAllRaw" />/<see cref="ItemReader.ReadAll" /> each re-read and re-decode
///     the ~600 KB file from scratch, so sharing one parse across every test in the class keeps the suite fast.
/// </summary>
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
