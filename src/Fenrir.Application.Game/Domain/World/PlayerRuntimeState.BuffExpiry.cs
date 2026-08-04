using Fenrir.Application.Game.Domain.Buffs;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    public void RepairExpiredBuffRuntimeState(ReadOnlySpan<int> changedSlots)
    {
        if (changedSlots[BuffCatalog.DarkAttack] == BuffCatalog.RemovedStateMarker)
        {
            DarkAttackKind = 0;
            DarkAttackUseTick = 0;
            DarkAttackActiveTick = 0;
        }

        if (changedSlots[BuffCatalog.HitRatePotion] == BuffCatalog.RemovedStateMarker)
        {
            HitRateKind = 0;
            HitRateTick = 0;
        }

        if (changedSlots[BuffCatalog.DodgeRatePotion] == BuffCatalog.RemovedStateMarker)
        {
            DodgeRateKind = 0;
            DodgeRateTick = 0;
        }
    }
}
