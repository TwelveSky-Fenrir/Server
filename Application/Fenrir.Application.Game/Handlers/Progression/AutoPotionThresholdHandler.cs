using Fenrir.Application.Game.Handlers.Progression.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>CZ_CHANGE_AUTO_INFO (opcode 86) -- persists auto-potion HP/MP thresholds; silent on success (verified).</summary>
public sealed class AutoPotionThresholdHandler(IAutoPotionThresholdService autoPotionThresholdService)
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

        var result = await autoPotionThresholdService.ApplyAsync(characterId, state, packet.Value01, packet.Value02,
            cancellationToken);

        if (result.Aborted)
            zoneSession.Abort(DisconnectReason.Faulted);
    }
}
