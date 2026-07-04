using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.Social;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
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
public sealed class GuildInviteHandler(GuildInviteRegistry invites) : IInlinePacketHandler<GuildInviteRequest>
{
    public void Handle(in GuildInviteRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var askerId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(askerId, out var asker) || asker is null)
            return;

        if (asker.GuildId is null || !GuildRoleCodec.IsMasterOrSubMaster(asker.GuildRoleDb))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, packet.AvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
        {
            session.Send(new GuildInviteAnswerResponse { Answer = 4 });
            return;
        }

        if (target.GuildId is not null)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (asker.Tribe != target.Tribe)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        switch (invites.TryAsk(askerId, target.CharacterId))
        {
            case GuildInviteAskOutcome.AskerBusy:
                session.Send(new GuildInviteAnswerResponse { Answer = 3 });
                return;
            case GuildInviteAskOutcome.TargetBusy:
                session.Send(new GuildInviteAnswerResponse { Answer = 5 });
                return;
            case GuildInviteAskOutcome.Sent:
                target.Session.Send(new GuildInviteResponse { AvatarName = asker.Name });
                return;
        }
    }
}
