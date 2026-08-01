using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterCostumeSlot")]
public sealed partial record CharacterCostumeSlotTvp(
    int CharacterId,
    byte Slot,
    int ItemId,
    int ItemDate,
    int ExpireDate);
