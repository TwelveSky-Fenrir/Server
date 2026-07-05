using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>CZ_CHANGE_AUTO_INFO (opcode 86) -- persists auto-potion HP/MP thresholds; silent on success (verified).</summary>
public sealed class AutoPotionThresholdHandler(ICharacterRepository characters)
    : IAsyncPacketHandler<AutoPotionThresholdRequest>
{
    public async ValueTask HandleAsync(AutoPotionThresholdRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (packet.Value01 is < 0 or > 5 || packet.Value02 is < 0 or > 5)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var lifeRatio = (byte)packet.Value01;
        var manaRatio = (byte)packet.Value02;

        await characters.SetAutoPotionThresholdAsync(characterId, lifeRatio, manaRatio, cancellationToken);

        // Written directly, not EconomyActionLock-guarded: own-character scalar, no item/money involved.
        state.AutoLifeRatio = lifeRatio;
        state.AutoManaRatio = manaRatio;
    }
}
