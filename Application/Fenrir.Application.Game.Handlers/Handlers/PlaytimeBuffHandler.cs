using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

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
/// <remarks>
///     Every call also clobbers <see cref="PlayerRuntimeState.PlayTime2" /> to a fixed 300, valid Sort or not
///     -- op97's second, independent legacy defect (it destroys whatever the tick loop's
///     <c>PlayTimeAccrualSystem</c> had accumulated in that same field). Reproduced deliberately, not fixed:
///     see <see cref="PlaytimeBuffResolver" />'s remarks and <c>PlaytimeBuffService</c>'s remarks for the D8
///     rationale.
/// </remarks>
public sealed class PlaytimeBuffHandler(IPlaytimeBuffService service, ILogger<PlaytimeBuffHandler> logger)
    : IInlinePacketHandler<PlaytimeBuffRequest>
{
    public void Handle(in PlaytimeBuffRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        logger.LogDebug(
            "Session {SessionId}: PlaytimeBuffRequest (op97) received for character {CharacterId}, sort {Sort}",
            session.SessionId, characterId, packet.Sort);

        var result = service.Apply(zone, characterId, packet.Sort);

        logger.LogInformation("Character {CharacterId} playtime-buff sort {Sort} resolved to value {Value}",
            characterId, packet.Sort, result.Value);

        session.Send(new AvatarStatUpdateResponse { Sort = 55, Value = result.Value, Value2 = 0 });
    }
}
