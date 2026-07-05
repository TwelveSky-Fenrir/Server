using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Modern replacement for the legacy op94 hop (ZCP_BROADCAST_INFO_RECV, ZC_BROADCAST_INFO_RECV a.k.a.
///     <see cref="ZoneEventInfoResponse" />): every legacy ts25zone process learned of a
///     <c>mWorldInfo</c>/<c>mTribeInfo</c> mutation from a separate ts25center hop
///     (ZCP_ZONE_BROADCAST_FOR_CENTER_SEND -&gt; <c>MyGame::ProcessForBroadcastInfo</c>) before relaying the
///     client-facing notice onward. Fenrir runs every zone in this one process against the one shared
///     <see cref="WorldStateService" /> instance, so there is no inter-process hop left to model -- this
///     class collapses "mutate the shared state" and "tell every connected client what changed" into a
///     single call.
///     <para>
///         Only the RvR sorts <see cref="WorldStateService" /> actually models are covered here
///         (S07_MyGame08.cpp cases 38/40/42/45/46 -- see that class's own remarks for which legacy fields have
///         no backing column yet). The many other legacy sorts sharing this same wire opcode (guild notices,
///         the numbered zone-siege state machines) belong to their own features.
///     </para>
/// </summary>
public sealed class ZoneEventBroadcaster(
    WorldStateService worldState,
    ZoneRegistry zones,
    ILogger<ZoneEventBroadcaster> logger)
{
    private const int DataSize = 130;

    /// <summary>tSort 38 (S04_MyWork02.cpp case 38 equivalent): a tribe has just decided Zone038.</summary>
    public void AnnounceZone038Winner(byte tribeId)
    {
        worldState.SetZone038Winner(tribeId);
        Broadcast(38, tribeId);
    }

    /// <summary>tSort 40: the tribe-symbol (HSB) battle window opens; every tribe starts back on its own symbol.</summary>
    public void AnnounceTribeSymbolBattleStarted()
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolBattlePhase}", "Started");

        worldState.StartTribeSymbolBattle();
        Broadcast(40);

        logger.LogInformation("Tribe symbol battle opened; every tribe reset to its own symbol");
    }

    /// <summary>tSort 45: the tribe-symbol (HSB) battle window closes.</summary>
    public void AnnounceTribeSymbolBattleEnded()
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolBattlePhase}", "Ended");

        worldState.EndTribeSymbolBattle();
        Broadcast(45);

        logger.LogInformation("Tribe symbol battle closed");
    }

    /// <summary>
    ///     tSort 42: one symbol slot is resolved. <paramref name="symbolIndex" /> 0-3 is a tribe's own slot
    ///     (<paramref name="winnerTribeId" /> only decides whether that tribe keeps it); 4 is the neutral,
    ///     monster-guarded slot.
    /// </summary>
    public void AnnounceSymbolResolved(byte symbolIndex, byte winnerTribeId)
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolIndex} {WinnerTribeId}", symbolIndex, winnerTribeId);

        if (symbolIndex == WorldStateService.TribeCount)
            worldState.ResolveMonsterSymbol(winnerTribeId);
        else
            worldState.ResolveTribeSymbol(symbolIndex, winnerTribeId);

        Broadcast(42, symbolIndex, winnerTribeId);

        logger.LogInformation("Symbol {SymbolIndex} resolved to tribe {WinnerTribeId}", symbolIndex, winnerTribeId);
    }

    /// <summary>
    ///     tSort 46: a tribe alliance offer is set. The legacy's 3-sort propose/accept/reject-with-date
    ///     distinction (S07_MyGame08.cpp cases 46/47/49) collapses onto <see cref="WorldStateService" />'s flat
    ///     (from, to, accepted) model -- this announces whichever of those this call just wrote.
    /// </summary>
    public void AnnounceAllianceOffer(byte fromTribeId, byte toTribeId, bool isAccepted)
    {
        worldState.SetAllianceOffer(fromTribeId, toTribeId, isAccepted);
        Broadcast(46, fromTribeId, toTribeId, isAccepted ? 1 : 0);
    }

    private void Broadcast(int sort, params ReadOnlySpan<int> fields)
    {
        var data = new byte[DataSize];
        for (var i = 0; i < fields.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i * 4), fields[i]);

        var response = new ZoneEventInfoResponse { Sort = sort, Data = data };

        foreach (var zone in zones.Zones)
        foreach (var player in zone.Players)
            player.Session.Send(response);
    }
}
