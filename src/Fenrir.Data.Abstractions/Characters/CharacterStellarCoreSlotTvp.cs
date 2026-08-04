using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterStellarCoreSlot")]
public sealed partial record CharacterStellarCoreSlotTvp(
    int CharacterId,
    byte Slot,
    int ItemId);
