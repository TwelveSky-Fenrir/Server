using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

// Le mécanisme de migration d'entité en mémoire (ZoneTransfer.Request/CreateEnterData) a été retiré avec
// l'unification V2.2 : un changement de zone ne migre plus l'état en RAM, il passe par ticket + reconnexion +
// ré-hydratation depuis SQL (cf. ZoneMoveService / EnterWorldService / Zone.PlayerLifecycle.HandleLeave).
// Seule la règle de purge de buffs à l'entrée d'une zone spéciale reste ici (consommée par le cross-shard).
public static class ZoneTransferBuffRules
{
    public const short BuffClearDestinationZoneId = 124;

    public static BuffInfo Resolve(BuffInfo liveBuffs, short targetMapId)
    {
        return targetMapId == BuffClearDestinationZoneId
            ? new BuffInfo { Buff = new int[70] }
            : new BuffInfo { Buff = (int[])liveBuffs.Buff.Clone() };
    }

    public static void ClearIfDestinationRequiresIt(BuffInfo liveBuffs, short targetMapId)
    {
        if (targetMapId == BuffClearDestinationZoneId)
            Array.Clear(liveBuffs.Buff);
    }
}
