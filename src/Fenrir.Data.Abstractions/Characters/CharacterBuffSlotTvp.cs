using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterBuffSlot")]
public sealed partial record CharacterBuffSlotTvp(
    int CharacterId,
    byte SlotIndex,
    int Value,
    int RemainingLegacyTicks);
