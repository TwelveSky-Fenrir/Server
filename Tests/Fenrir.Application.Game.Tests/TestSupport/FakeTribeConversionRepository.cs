using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeTribeConversionRepository : ITribeConversionRepository
{
    public (int CharacterId, int ItemId, byte ToTribe, byte Container, IReadOnlyList<CharacterItemSlotTvp> Items)?
        LastCall { get; private set; }

    public bool ThrowOnApply { get; set; }

    public ValueTask ApplyTribeScrollConversionAsync(int characterId, int itemId, byte toTribe, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        if (ThrowOnApply)
            throw new InvalidOperationException("FakeTribeConversionRepository: ThrowOnApply is set.");

        LastCall = (characterId, itemId, toTribe, container, items);
        return ValueTask.CompletedTask;
    }
}
