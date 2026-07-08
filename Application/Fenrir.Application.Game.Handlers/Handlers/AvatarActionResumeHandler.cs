using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Logging;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_UPDATE_AVATAR_ACTION (op16) -- same wire payload shape as <see cref="AvatarActionHandler" /> (op15,
///     both ride <c>ActionInfo</c>) and the same <see cref="IAvatarActionService.PostAction" /> entry point,
///     but NOT the same Sort/Type legality gate: op16 runs its own, separate inline switch in the legacy
///     source (Server/ts25zone/S04_MyWork02.cpp:1815-1878), materially more permissive than op15's own
///     whitelist (<c>CheckValidCharacterMotionForSend</c>, whose sole call site is op15's handler,
///     S04_MyWork02.cpp:1544). <c>isResumeAction: true</c> below is what routes this opcode's traffic to
///     <see cref="Fenrir.Application.Game.Domain.Movement.AvatarActionResumeWhitelist" /> instead of
///     <see cref="Fenrir.Application.Game.Domain.Movement.CharacterMotionWhitelist" /> once the command
///     reaches <c>Zone.PlayerLifecycle.HandleMove</c> -- misapplying op15's table here previously caused
///     legitimate op16 packets (Sort 19, 31, 92-95 among others) to be wrongly hard-disconnected.
/// </summary>
public sealed class AvatarActionResumeHandler(
    IAvatarActionService service,
    ILogger<AvatarActionResumeHandler>? logger = null) : IInlinePacketHandler<AvatarActionResumeRequest>
{
    public void Handle(in AvatarActionResumeRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var action = packet.Action;
        var characterId = zoneSession.CharacterId!.Value;

        logger?.AvatarActionReceived(session.SessionId, characterId, 16, action.Type, action.Sort);

        service.PostAction(zone, characterId, in action, true);
    }
}
