using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

public static class ZoneTransferBuffRules
{
    public const short BuffClearDestinationZoneId = 124;

    public static void ClearIfDestinationRequiresIt(BuffInfo liveBuffs, short targetMapId)
    {
        if (targetMapId == BuffClearDestinationZoneId)
            Array.Clear(liveBuffs.Buff);
    }
}
