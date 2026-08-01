using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain.World;

public enum KillCpType : byte
{
    Stun = 0,

    NormalHit = 1,

    CriticalHit = 2
}

public static class OneShotKillClassifier
{
    // Server/ts25zone/S07_MyGame02.cpp:546-614 (AtkGet1Hit), called live at :634
    private static readonly FrozenSet<int> OneShotSkillIndices = new[]
    {
        8, 12, 16, 27, 31, 35, 46, 50, 54, 58, 61, 63, 65, 67, 69, 71, 73, 75,
        85, 87, 89, 91, 93, 95, 97, 99, 101, 121, 123, 125, 127, 129, 131, 133, 135, 137
    }.ToFrozenSet();

    public static bool IsOneShotSkill(int skillIndex)
    {
        return OneShotSkillIndices.Contains(skillIndex);
    }

    public static KillCpType Classify(int killingSkillIndex, bool isReflectKill)
    {
        if (isReflectKill)
            return KillCpType.CriticalHit;

        return IsOneShotSkill(killingSkillIndex)
            ? KillCpType.CriticalHit
            : KillCpType.NormalHit;
    }
}

public partial class Zone
{
    private bool IsWarZone049Type => RegularWarMapCatalog.TryGet(MapId, out _);

    internal bool IsFormationSkillZoneLocked(int skillNumber)
    {
        return FormationSkillCatalog.IsFormationSkillZoneLocked(skillNumber, MapId, IsWarZone049Type);
    }

    internal void AdvanceCasterPartyBuffMarker(PlayerRuntimeState state, int skillNumber, int actionSort)
    {
        state.PartyBuffAct = FormationSkillCatalog.NextPartyBuffMarker(state.PartyBuffAct, skillNumber, actionSort);
    }

    internal static void ResetPartyBuffMarker(PlayerRuntimeState state)
    {
        state.PartyBuffAct = PartyBuffAction.None;
    }

    internal KillCpType ClassifyPvpKillType(int killingSkillIndex, bool isReflectKill)
    {
        return OneShotKillClassifier.Classify(killingSkillIndex, isReflectKill);
    }
}
