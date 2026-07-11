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

        private static readonly FrozenSet<int> OneShotSkillIndices = FrozenSet<int>.Empty;

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
