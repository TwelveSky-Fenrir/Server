using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Per-tick maintenance for every character currently recorded as being in an Active duel
///     (<see cref="DuelRegistry" />'s own ask/accept/start handshake having already completed) -- the
///     opponent-not-found, opponent-died, self-died, and 180-legacy-tick auto-timeout end conditions the
///     handshake itself never resolves on its own.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame04.cpp:595-682 (<c>AVATAR_OBJECT::Update</c>'s per-tick duel
///     maintenance block: opponent-not-found / opponent-dead / self-dead / remaining-time-expired checks, in
///     that priority order, and the <c>RESET_DUEL</c> cleanup macro at :606-611 applied to both participants
///     on every end condition) ; Server/ts25zone/S07_MyGame04.cpp:602 (<c>mSERVER_INFO.mServerNumber != 124</c>
///     guard -- this entire block never runs on map 124, which runs its own independent team-arena
///     reset-on-death system instead, S07_MyGame04.cpp:723-748, confirmed still compiled in production by the
///     unconditional <c>ZONE124</c> macro at H07_MyGame.h:19 -- reproduced below as an explicit
///     <see cref="Zone.MapId" /> guard, matching every other per-map gate in this codebase
///     (e.g. <c>ZonePvpZoneCatalog</c>) rather than omitting the system per zone) ;
///     Server/ts25zone/S04_MyWork02.cpp:8400-8454
///     (DUEL_START_SEND seeds the 180-unit remaining-time counter) ; Server/ts25zone/S07_MyGame03.cpp:4422-4452
///     (<c>MyUtil::SearchAvatar</c> -- the exact-name, no-distance-filter "opponent still present" test the
///     legacy uses; re-expressed here as an exact-zone-membership test via <see cref="Zone.TryGetPlayer" />,
///     equivalent since a duel's two participants are always resolved within the asker's own zone at Ask time)
///     ; ServerDocs/12_ts25zone/13_MyGame04_06_07_Avatar_Item_Tick.md §2.5.5 (reason-code mapping: 0 = time
///     expired/draw, 1 = opponent died/win, 2 = self died/lose, 3 = opponent not found -- mirrored exactly by
///     <see cref="DuelEndReason" />'s own numeric values).
///     The legacy resolves this asymmetrically per-participant (only the "case 1"/initiator side's own
///     per-tick pass runs the full four-condition resolution; the "case 2" side's own pass only handles its
///     own opponent-not-found sub-case, per §2.5.5's own note) -- whose net externally observable effect is
///     nonetheless symmetric: both participants are always reset together. This is re-expressed here as a
///     single canonical resolution keyed off <see cref="ActiveDuel.PlayerA" /> ("the initiator's own side")
///     for the death tie-break, processed once per duel per tick (not once per participant, via the
///     <c>processedDuelIds</c> dedupe set below) -- both because a duel's two participants normally share
///     this same zone and would otherwise double-decrement <see cref="ActiveDuel.RemainingTicks" /> or
///     double-fire the end notification, and because it gives a deterministic winner on a same-tick double
///     death regardless of <see cref="Zone.Players" />'s own enumeration order.
/// </remarks>
public sealed class DuelMaintenanceSystem(DuelRegistry duels) : ISimulationSystem
{
    /// <summary>
    ///     Map 124 (scripted-duel/team-arena server) -- CZ_DUEL_ASK_SEND already refuses outright here, so no active duel
    ///     ever exists to maintain, but the guard is kept explicit to match the legacy's own (S07_MyGame04.cpp:602).
    /// </summary>
    private const short ScriptedDuelArenaMapId = 124;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (zone.MapId == ScriptedDuelArenaMapId)
            return;

        HashSet<int>? processedDuelIds = null;

        foreach (var state in zone.Players)
        {
            if (!duels.TryGetActiveDuel(state.CharacterId, out var duel) || duel is null)
                continue;

            // Already resolved this tick by the other participant's own pass through this same loop.
            if (!(processedDuelIds ??= []).Add(duel.UniqueNumber))
                continue;

            Resolve(zone, duel, legacyTicksElapsed);
        }
    }

    private void Resolve(Zone zone, ActiveDuel duel, int legacyTicksElapsed)
    {
        zone.TryGetPlayer(duel.PlayerA, out var playerA);
        zone.TryGetPlayer(duel.PlayerB, out var playerB);

        // Opponent no longer found in this zone (disconnected, logged out, or transferred elsewhere): ends
        // immediately, draw-like -- only whichever side is still present gets cleaned up/notified.
        if (playerA is null || playerB is null)
        {
            duels.TryEndActiveDuel(duel.PlayerA, out _);
            if (playerA is not null)
                zone.EndActiveDuel(playerA, DuelEndReason.OpponentNotFound);
            if (playerB is not null)
                zone.EndActiveDuel(playerB, DuelEndReason.OpponentNotFound);
            return;
        }

        // Opponent-dead is checked before self-dead: a same-tick double death always resolves as a win for
        // PlayerA's side first, never a true double-loss/draw.
        if (playerB.IsDead)
        {
            EndBoth(zone, duel, playerA, playerB);
            return;
        }

        if (playerA.IsDead)
        {
            EndBoth(zone, duel, playerB, playerA);
            return;
        }

        duel.RemainingTicks -= legacyTicksElapsed;
        if (duel.RemainingTicks <= 0)
        {
            duels.TryEndActiveDuel(duel.PlayerA, out _);
            zone.EndActiveDuel(playerA, DuelEndReason.TimeExpired);
            zone.EndActiveDuel(playerB, DuelEndReason.TimeExpired);
            return;
        }

        playerA.Session.Send(new DuelCountdownResponse { RemainTime = duel.RemainingTicks });
        playerB.Session.Send(new DuelCountdownResponse { RemainTime = duel.RemainingTicks });
    }

    private void EndBoth(Zone zone, ActiveDuel duel, PlayerRuntimeState winner, PlayerRuntimeState loser)
    {
        duels.TryEndActiveDuel(duel.PlayerA, out _);
        zone.EndActiveDuel(winner, DuelEndReason.OpponentDied);
        zone.EndActiveDuel(loser, DuelEndReason.SelfDied);
    }
}
