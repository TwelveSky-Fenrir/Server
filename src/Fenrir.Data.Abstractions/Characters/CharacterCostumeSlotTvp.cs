using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

// Slots OCCUPES seulement : absence de ligne = slot vide, meme convention que CharacterRuneSocketTvp.
// ItemDate empaquete enchant/combine/refine/socket ; ExpireDate est l'entier legacy YYYYMMDD verbatim.
[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterCostumeSlot")]
public sealed partial record CharacterCostumeSlotTvp(
    int CharacterId,
    byte Slot,
    int ItemId,
    int ItemDate,
    int ExpireDate);
