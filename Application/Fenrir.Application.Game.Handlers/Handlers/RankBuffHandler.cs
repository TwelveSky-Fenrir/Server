using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>
///     CZ_RANK_BUFF_SEND (op111). On success, heals Life/Mana to max and replies via a self-unicast
///     <see cref="AvatarStatUpdateResponse" /> echoing the new <c>aRankBuffType</c>. See
///     <see cref="RankBuffResolver" />'s remarks for the stone-count gating (only Sort=1 is reachable today).
/// </summary>
/// <remarks>
///     The legacy's mid-zone-transfer guard (<c>IsMovingZone()</c>) has no Fenrir equivalent: this codebase's
///     zone transfer is a single-tick handoff (the departing player is simply absent from
///     <see cref="Zone.TryGetPlayer" /> once <c>Leave</c> drains), so there is no observable "moving" window to
///     gate against. <c>MyFactor.cpp</c>'s per-<c>aRankBuffType</c> stat bonuses (7 separate formula sites) are
///     not modeled -- only the wire mechanic, the state mirror, and the HP/MP heal are implemented.
/// </remarks>
public sealed class RankBuffHandler(IRankBuffService service) : IInlinePacketHandler<RankBuffRequest>
{
    public void Handle(in RankBuffRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = service.Apply(zone, state, characterId, packet.Sort);
        if (!result.Succeeded)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new AvatarStatUpdateResponse { Sort = 68, Value = packet.Sort, Value2 = 0 });
    }
}
