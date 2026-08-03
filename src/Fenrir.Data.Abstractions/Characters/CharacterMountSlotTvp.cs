using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterMountSlot")]
public sealed partial record CharacterMountSlotTvp(
    int CharacterId,
    byte Slot,
    int ItemId,
    int ExpActivity,
    int Power);
