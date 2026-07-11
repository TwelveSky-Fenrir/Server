using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Tribes;

namespace Fenrir.Application.Game.Tests.TestSupport;

// In-memory stand-in for ITribeConversionRepository (the C11 faction-transfer scroll's atomic
// equip/skill/hotkey remap + tribe flip + scroll consumption). Records the last call's arguments so a test can
// assert on exactly what TribeScrollTransferUseItemHandler asked the stored procedure to do.
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
