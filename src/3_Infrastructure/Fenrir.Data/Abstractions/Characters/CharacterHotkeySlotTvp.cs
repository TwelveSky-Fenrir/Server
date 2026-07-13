using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterHotkeySlot")]
public sealed partial record CharacterHotkeySlotTvp(
    byte Page,
    byte KeyIndex,
    int Sort,
    int Value1,
    int Value2);
