using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_RANK_BUFF_SEND (op111). On success, heals Life/Mana to max and replies via a self-unicast
///     <see cref="AvatarStatUpdateResponse" /> echoing the new <c>aRankBuffType</c>. See
///     <see cref="RankBuffResolver" />'s remarks for the stone-count gating (only Sort=1 is reachable today) and
///     for the two wire preconditions (mid-zone-transfer disconnect, world-battle silent no-op) this handler
///     delegates to it.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:13994-14112 (progression-and-misc-systems behavior contract,
///     2026-07 round). Both preconditions this handler's own remarks previously tracked as open follow-ups --
///     the acting avatar's own <see cref="PlayerRuntimeState.IsMovingZone" /> disconnect gate and the
///     world-wide tribe-symbol-battle silent no-op -- are now closed via <see cref="RankBuffResolver.Resolve" />;
///     see that type's remarks for the full citation trail, including why the world-battle flag is no longer
///     "always 0 in Fenrir" as this file previously claimed. <c>MyFactor.cpp</c>'s per-<c>aRankBuffType</c> stat
///     bonuses (7 separate formula sites) are still not modeled -- only the wire mechanic, the state mirror, and
///     the HP/MP heal are implemented.
/// </remarks>
public sealed class RankBuffHandler(IRankBuffService service, ILogger<RankBuffHandler> logger)
    : IInlinePacketHandler<RankBuffRequest>
{
    public void Handle(in RankBuffRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: RankBuffRequest (op111) received for character {CharacterId}, sort {Sort}",
                session.SessionId, characterId, packet.Sort);

        var result = service.Apply(zone, state, characterId, packet.Sort);

        if (result.SilentlyIgnored)
        {
            // World-wide tribe-symbol (RvR) battle is active -- complete silent no-op: no reply, no disconnect,
            // no state change, distinct from every other rejection below (RankBuffResolver.Outcome.WorldBattleActive).
            logger.LogDebug(
                "Rank-buff silently ignored for character {CharacterId}: world tribe-symbol battle in progress",
                characterId);
            return;
        }

        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Rank-buff rejected for character {CharacterId}: sort {Sort} (mid zone-transfer, out-of-range tier, or insufficient territory-symbol count) -- aborting session",
                characterId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        logger.LogInformation("Character {CharacterId} applied rank buff sort {Sort}", characterId, packet.Sort);

        session.Send(new AvatarStatUpdateResponse { Sort = 68, Value = packet.Sort, Value2 = 0 });
    }
}
