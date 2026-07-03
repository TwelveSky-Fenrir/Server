using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Characters;

/// <summary>
///     One occupied slot of ONE container for usp_CharacterItems_ReplaceContainer -- mirrors
///     game.tvp_CharacterItemSlot's column order 1:1 (CharacterId/Container are proc scalars, not TVP columns: the
///     replace unit is a whole container, the way the legacy saves each avatar item array as a block). Same
///     [GenerateTvp] streaming rationale as <see cref="CharacterPositionTvp" />.
/// </summary>
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
