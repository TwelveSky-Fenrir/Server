using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     "Hoisundo" forced-departure countdown for the rebirth-event zones 234-240: once per elapsed real
///     minute, decrements <see cref="PlayerRuntimeState.HoisundoTimeRemaining" /> for every ready,
///     non-zone-transferring avatar in one of those zones, broadcasts the new value to that avatar's own
///     session, and disconnects it once the countdown drops below one.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:1748-1865 (the <c>mServerNumber &gt;= 234 &amp;&amp;
///     &lt;= 240</c> gate and the one-case-per-zone-number switch, each decrementing its own
///     <c>aZoneNNNTime</c> avatar field, broadcasting via <c>B_AVATAR_CHANGE_INFO_2</c>, and calling
///     <c>user-&gt;Quit()</c> once the value drops below 1) ; Server/Header/Protocol/STRUCT.h:1548-1554
///     (<c>S033ZONE_234_TIME</c>..<c>S039ZONE_240_TIME</c> broadcast sort codes, one per zone number) ;
///     Server/Header/Protocol/ZONE.h:453-460 and Server/Header/Protocol/DEFINE.h:63
///     (<c>ZC_AVATAR_CHANGE_INFO_2</c> wire shape -- Sort/Value/Value2, <c>USE_PREMIUM_LONGTIME</c>
///     unconditionally live so Value2 is always present -- the legacy struct this codebase's
///     <see cref="AvatarStatUpdateResponse" /> already models) ; ServerDocs/12_ts25zone/08_MyGame01_PartieA.md
///     §2.1.
///     <para>
///         <b>Single-decrement-per-boundary catch-up</b>, not the "full multi-minute catch-up" every other
///         once-per-real-minute system in this cluster (<see cref="PlayTimeAccrualSystem" />,
///         <see cref="PetExpBoostCountdownSystem" />, <see cref="SupportSkillTimeUpRatioMaintenanceSystem" />)
///         uses: legacy's own gate resets its baseline tick to "now" on every fire
///         (<c>sHoisundoMinuteTick = tGetTickCount;</c>, S07_MyGame01.cpp:1759), never to "+1 minute", so a
///         stalled host only ever loses at most one decrement here per call, the remainder is simply not
///         caught up -- the same "accumulate legacy ticks, subtract the per-minute threshold once and carry
///         the remainder" idiom <see cref="PetActivitySystem" /> already uses for its own less time-critical
///         decay, not the "while-loop multiply by whole minutes elapsed" idiom the three systems above use.
///     </para>
///     <para>
///         <b>Gate</b>: a literal <see cref="Zone.MapId" />-range check (234-240 inclusive), not an
///         operator-configured map-id set like <see cref="GameServerOptions.Zone241DungeonMapIds" /> --
///         unlike the Zone-241 personal-dungeon family (whose legacy server-number list spans many non-241
///         numbers with no per-number distinction needed), this mechanic's own C++ switches on the *exact*
///         server number to pick a distinct avatar field and broadcast sort code per zone, and this codebase
///         already assumes a zone's <c>MapId</c> equals its legacy zone/server number one-to-one (Zone.cs's
///         own <c>TryLoadGeometry</c>, resolving world geometry from <c>Z{mapId:D3}.WM</c>) -- so no extra
///         indirection is introduced for a family that was always 1:1 in the source material.
///     </para>
///     <para>
///         <b>Deliberately not implemented</b> (no behavior contract covers either, and guessing would
///         violate the "no legacy parity from memory" rule): the "zone key" item-consumption mechanic that
///         grants/extends the countdown before or during a visit (Server/ts25zone/S04_MyWork03.cpp:6130-6237,
///         Server/ts25zone/S07_MyGame04.cpp:5697-5806), and wherever the legacy DB column seeds its starting
///         value on character load -- see <see cref="PlayerRuntimeState.HoisundoTimeRemaining" />'s own
///         remarks for the resulting "starts pre-exhausted in production" posture this system inherits until
///         a follow-up contract supplies one or both.
///     </para>
/// </remarks>
public sealed class HoisundoCountdownSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (ResolveBroadcastSort(zone.MapId) is not { } sort)
            return;

        List<PlayerRuntimeState>? toDisconnect = null;

        foreach (var state in zone.Players)
        {
            // Server/ts25zone/S07_MyGame01.cpp:1770-1773: MyUser::IsMovingZone() skip -- mirrored here by
            // PlayerRuntimeState.IsMovingZone, same "cross-shard zone transfer in flight" gate every other
            // per-tick system in this cluster already honors.
            if (state.IsMovingZone)
                continue;

            state.HoisundoAccrualTicks += legacyTicksElapsed;
            if (state.HoisundoAccrualTicks < SimulationClock.PlayTimeAccrualLegacyTicks)
                continue;

            state.HoisundoAccrualTicks -= SimulationClock.PlayTimeAccrualLegacyTicks;

            if (state.HoisundoTimeRemaining > 0)
            {
                state.HoisundoTimeRemaining--;
                state.Session.Send(new AvatarStatUpdateResponse
                    { Sort = sort, Value = state.HoisundoTimeRemaining, Value2 = 0 });
            }

            if (state.HoisundoTimeRemaining < 1)
                (toDisconnect ??= []).Add(state);
        }

        if (toDisconnect is null)
            return;

        // Deferred to after the full player scan, same posture as DeathGateTickSystem's own toForceQuit list:
        // Abort() only requests teardown (it does not remove the player from zone.Players synchronously), so
        // disconnecting mid-enumeration here would risk mutating the very collection this loop is iterating.
        foreach (var state in toDisconnect)
            if (state.Session is ClientSession client)
                client.Abort(DisconnectReason.TimedZoneExpired);
    }

    /// <summary>S033ZONE_234_TIME..S039ZONE_240_TIME (Server/Header/Protocol/STRUCT.h:1548-1554), one per zone number.</summary>
    private static int? ResolveBroadcastSort(short mapId)
    {
        return mapId switch
        {
            234 => 33,
            235 => 34,
            236 => 35,
            237 => 36,
            238 => 37,
            239 => 38,
            240 => 39,
            _ => null
        };
    }
}
