using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Abstractions.Tribes;

public interface ITribeConversionRepository
{

        public ValueTask ApplyTribeScrollConversionAsync(int characterId, int itemId, byte toTribe, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);
}
