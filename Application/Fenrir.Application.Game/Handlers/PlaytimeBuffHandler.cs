using Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_TIME_EFFECT_SEND (op97). Always replies (even a no-op Sort echoes Value=-1) via a self-unicast
///     <see cref="AvatarStatUpdateResponse" /> -- see <see cref="PlaytimeBuffResolver" />'s remarks for why every
///     Sort in [1,5] always succeeds in this build.
/// </summary>
/// <remarks>
///     <c>SetTimeEffect</c>'s downstream drop/exp-rate multipliers (mItemDropUpRatio, mGeneralExpUpRatio,
///     mPetExpUpRatio, mKillOtherTribeAddValue, mMountExpUpRatio) are not modeled: no per-character economy
///     multiplier exists anywhere in Fenrir's loot/exp pipeline yet (<c>MonsterDropRoller</c>'s own remarks
///     explicitly disclaim per-character drop-rate modifiers), so applying them here would have no observable
///     effect. Only the wire mechanic and <see cref="PlayerRuntimeState.StateTimeEffect" /> mirror are
///     implemented.
/// </remarks>
public sealed class PlaytimeBuffHandler(IPlaytimeBuffService service) : IInlinePacketHandler<PlaytimeBuffRequest>
{
    public void Handle(in PlaytimeBuffRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = service.Apply(zone, characterId, packet.Sort);

        session.Send(new AvatarStatUpdateResponse { Sort = 55, Value = result.Value, Value2 = 0 });
    }
}
