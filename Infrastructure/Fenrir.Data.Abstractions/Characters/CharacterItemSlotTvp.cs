using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

// Mirrors game.tvp_CharacterItemSlot order; CharacterId/Container are proc scalars, not TVP columns -- replace unit is a whole container (legacy per-array save block).
[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterItemSlot")]
public sealed partial record CharacterItemSlotTvp(
    byte Slot,
    int ItemId,
    int Quantity,
    byte Enchant,
    byte Combine,
    byte Refine,
    byte Socket,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3,
    int ExpireDate,
    int Serial);
