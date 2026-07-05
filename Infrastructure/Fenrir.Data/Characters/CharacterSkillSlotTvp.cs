using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Characters;

// Mirrors game.tvp_CharacterSkillSlot order; used only by usp_Character_CreateWithStarterKit today.
[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterSkillSlot")]
public sealed partial record CharacterSkillSlotTvp(
    byte SlotIndex,
    int SkillId,
    int Grade);
