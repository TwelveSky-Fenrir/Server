using Fenrir.Application.Game.Social.Duel;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_DUEL_START_SEND (opcode 46) -- callable by EITHER accepted side (see <see cref="DuelRegistry" />).
///     Scope cut: does not broadcast the legacy's ZC_AVATAR_CHANGE_INFO_1 (tSort=7) to nearby players, and
///     the 180 s countdown itself (ZC_DUEL_TIME_INFO ticks + auto-end) is not implemented.
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
