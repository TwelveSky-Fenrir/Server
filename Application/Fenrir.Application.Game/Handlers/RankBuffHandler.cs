using Fenrir.Application.Game.Buffs;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

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
public sealed class RankBuffHandler : IInlinePacketHandler<RankBuffRequest>
{
    /// <summary>
    ///     ReturnSymbolNumNoMon under a no-alliance, no-capture-event default world state -- see RankBuffResolver's
    ///     remarks.
    /// </summary>
    private const int DefaultStoneCount = 1;

    public void Handle(in RankBuffRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var resolved = RankBuffResolver.Resolve(packet.Sort, DefaultStoneCount);
        if (!resolved.Succeeded)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
        var maxMana = state.Stats?.MaxMana ?? state.MaxMana;

        session.Send(new AvatarStatUpdateResponse { Sort = 68, Value = packet.Sort, Value2 = 0 });

        zone.PostAvatarBuffCommand(new AvatarBuffZoneCommand(characterId, RankBuffType: packet.Sort, Life: maxLife,
            Mana: maxMana));
    }
}
