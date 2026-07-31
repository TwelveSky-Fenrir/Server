using CaeriusNet.Attributes.Tvp;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateTvp(Schema = "game", TvpName = "tvp_CharacterSkillSlot")]
public sealed partial record CharacterSkillSlotTvp(
    byte SlotIndex,
    int SkillId,
    int Grade);
