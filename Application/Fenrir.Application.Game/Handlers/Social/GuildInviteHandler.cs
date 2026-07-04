using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.Social;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_GUILD_ASK_SEND (opcode 72, doc 10 §0/§1, verified S04_MyWork02.cpp:9827-9878). Emitter must
///     already be in a guild with role 0 (master) or 1 (sub-master, wire encoding -- <see cref="GuildRoleCodec.IsMasterOrSubMaster" />
///     against the DB-side role) else <c>Quit()</c>. Target resolved WITHIN THE ASKER'S OWN ZONE ONLY
///     (<c>SearchAvatar</c> scope, doc 10 quirk 13) -- not found ⇒ ZC_GUILD_ANSWER_RECV Answer=4 to the
///     asker; already guilded or different tribe ⇒ <c>Quit()</c>; busy (already negotiating) ⇒ Answer=3/5.
///     Success ⇒ ZC_GUILD_ASK_RECV to the target.
/// </summary>
/// <remarks>
///     OPEN ISSUE: the legacy also gates on <c>CheckCommunityWork()</c>/action-sort 11-12 (stunned/dead)
///     and <c>IsMovingZone()</c> (target) -- neither has a <see cref="PlayerRuntimeState" /> equivalent, so
///     this is a documented gap, not a fabricated substitute.
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
