using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

// Mirrors game.tvp_CharacterHotkeySlot order; used only by usp_Character_CreateWithStarterKit today.
[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterHotkeySlot")]
public sealed partial record CharacterHotkeySlotTvp(
    byte Page,
    byte KeyIndex,
    int Sort,
    int Value1,
    int Value2);
