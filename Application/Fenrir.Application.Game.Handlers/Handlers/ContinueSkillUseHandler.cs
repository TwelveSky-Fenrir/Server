using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_CONTINUE_SKILL_USE_SEND (op95). Sort=1 starts auto-buff channeling (mana-gated, broadcasts the
///     resulting avatar action); Sort=2 is the per-tick buff-apply pulse -- a documented, wire-faithful no-op
///     here (the legacy itself never replies or disconnects for Sort=2 either; see
///     <see cref="AutoBuffActivationResolver" />'s remarks for what's out of scope). Any other Sort disconnects.
/// </summary>
public sealed class ContinueSkillUseHandler(IContinueSkillUseService service, ILogger<ContinueSkillUseHandler> logger)
    : IInlinePacketHandler<ContinueSkillUseRequest>
{
    public void Handle(in ContinueSkillUseRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        // Sort=2 is the per-tick auto-buff pulse (see this class's own remarks) -- fires on every tick while
        // channeling, so the entry log must not format/box unconditionally like the other handlers in this
        // area; gate behind IsEnabled exactly like SessionLoop.ProcessBufferAsync's own debugEnabled local.
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: ContinueSkillUseRequest (op95) received for character {CharacterId}, sort {Sort}",
                session.SessionId, characterId, packet.Sort);

        var result = service.Activate(zone, characterId, state, packet.Sort);

        switch (result.Kind)
        {
            case AutoBuffActivationResolver.ResultKind.NoReply:
            case AutoBuffActivationResolver.ResultKind.Tick:
                return;

            case AutoBuffActivationResolver.ResultKind.Disconnect:
                logger.LogWarning(
                    "Auto-buff activation rejected for character {CharacterId}: sort {Sort} -- aborting session",
                    characterId, packet.Sort);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;

            case AutoBuffActivationResolver.ResultKind.Activate:
                logger.LogInformation("Character {CharacterId} activated auto-buff channeling", characterId);
                session.Send(new AutoBuffActivationResponse { Value = 0 });
                return;
        }
    }
}
