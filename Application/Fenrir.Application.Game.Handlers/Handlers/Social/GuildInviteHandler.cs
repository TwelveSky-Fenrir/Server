using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_GUILD_ASK_SEND (opcode 72) -- emitter must be guild master or sub-master (DB-side role, see
///     <see cref="GuildRoleCodec.IsMasterOrSubMaster" />).
/// </summary>
/// <remarks>
///     <see cref="Fenrir.Application.Game.Services.Social.GuildInviteService.Ask" /> enforces the legacy
///     CheckCommunityWork()/stunned-dead exclusivity gate (asker checked before target resolution, target
///     checked again once found) via the sibling Duel/Trade/Friend/Party/Mentor/Guild negotiation registries
///     and <see cref="PlayerRuntimeState.IsStunned" />/<see cref="PlayerRuntimeState.IsDead" />.
///     OPEN ISSUE (still not modeled here): the target's own "currently mid zone-transfer" gate (legacy
///     <c>IsMovingZone()</c>). Since the zone-transfer-in-progress-gate behavior contract (2026-07 round),
///     <see cref="PlayerRuntimeState.IsMovingZone" /> now exists and is observably true for a target mid a
///     CROSS-shard transfer, but this specific guild-invite consuming site is one of the ~28 social sites
///     (<c>S04_MyWork02.cpp:8292-9950</c>) that contract explicitly left un-re-verified line-by-line --
///     wiring a guess at the exact failure-vs-silent-drop semantics/reply code here would risk a wrong wire
///     contract, so this remains open and tracked, not guessed at.
/// </remarks>
public sealed class GuildInviteHandler(
    IGuildInviteService guildInviteService,
    ILogger<GuildInviteHandler>? logger = null) : IInlinePacketHandler<GuildInviteRequest>
{
    public void Handle(in GuildInviteRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_GUILD_ASK_SEND received (character {CharacterId}, target {AvatarName})",
            session.SessionId, zoneSession.CharacterId, packet.AvatarName);

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
