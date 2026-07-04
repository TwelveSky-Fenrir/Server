using Fenrir.Application.Game.Social.Duel;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_DUEL_START_SEND (opcode 46) -- callable by EITHER accepted side (<see cref="DuelRegistry" />'s
///     own remarks). Allocates the duel's unique number, arms both sides, sends ZC_DUEL_START_RECV to
///     both with the 180 s timer. NOT sent (deliberate scope cut): the legacy ALSO broadcasts a
///     ZC_AVATAR_CHANGE_INFO_1 (tSort=7) to nearby players -- a separate, unrelated wire mechanism this
///     pass does not touch; the duel's own start notification (what both PARTICIPANTS see) is complete.
///     The 180 s countdown itself (ZC_DUEL_TIME_INFO ticks + auto-end) is NOT implemented -- see
///     <see cref="DuelRegistry" />'s own class remarks.
/// </summary>
public sealed class DuelStartHandler(ZoneRegistry zones, DuelRegistry duels) : IInlinePacketHandler<DuelStartRequest>
{
    private const int DurationSeconds = 180;

    public void Handle(in DuelStartRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var callerId = zoneSession.CharacterId!.Value;

        if (!duels.TryStart(callerId, out var duel))
            return;

        if (!zones.TryGetPlayer(duel.PlayerA, out var playerA) || !zones.TryGetPlayer(duel.PlayerB, out var playerB))
            return;

        var eatDrugState = duel.NoPotions ? 1 : 0;

        playerA.Session.Send(new DuelStartResponse
        {
            DuelState = [1, duel.UniqueNumber, 1],
            RemainTime = DurationSeconds,
            EatDrugState = eatDrugState
        });

        playerB.Session.Send(new DuelStartResponse
        {
            DuelState = [1, duel.UniqueNumber, 2],
            RemainTime = DurationSeconds,
            EatDrugState = eatDrugState
        });
    }
}
