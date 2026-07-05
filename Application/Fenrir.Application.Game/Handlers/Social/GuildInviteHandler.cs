using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.Social;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_GUILD_ASK_SEND (opcode 72) -- emitter must be guild master or sub-master (DB-side role, see
///     <see cref="GuildRoleCodec.IsMasterOrSubMaster" />).
/// </summary>
/// <remarks>
///     OPEN ISSUE: legacy also gates on CheckCommunityWork()/stunned-dead and target IsMovingZone(); neither
///     has a <see cref="PlayerRuntimeState" /> equivalent here.
/// </remarks>
public sealed class GuildInviteHandler(IGuildInviteService guildInviteService)
    : IInlinePacketHandler<GuildInviteRequest>
{
    public void Handle(in GuildInviteRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var askerId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(askerId, out var asker) || asker is null)
            return;

        switch (guildInviteService.Ask(zone, asker, packet.AvatarName))
        {
            case GuildInviteAskResultKind.NotAuthorized:
            case GuildInviteAskResultKind.TargetAlreadyGuilded:
            case GuildInviteAskResultKind.TribeMismatch:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case GuildInviteAskResultKind.TargetNotFound:
                session.Send(new GuildInviteAnswerResponse { Answer = 4 });
                return;
            case GuildInviteAskResultKind.AskerBusy:
                session.Send(new GuildInviteAnswerResponse { Answer = 3 });
                return;
            case GuildInviteAskResultKind.TargetBusy:
                session.Send(new GuildInviteAnswerResponse { Answer = 5 });
                return;
        }
    }
}
