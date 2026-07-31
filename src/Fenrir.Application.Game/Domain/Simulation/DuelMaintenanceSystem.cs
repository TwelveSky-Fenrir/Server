using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.Simulation;

public sealed class DuelMaintenanceSystem(DuelRegistry duels) : ISimulationSystem
{
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

            if (!(processedDuelIds ??= []).Add(duel.UniqueNumber))
                continue;

            Resolve(zone, duel);
        }
    }

    private void Resolve(Zone zone, ActiveDuel duel)
    {
        zone.TryGetPlayer(duel.PlayerA, out var playerA);
        zone.TryGetPlayer(duel.PlayerB, out var playerB);

        // RemainingTicks est un compte de SECONDES: il avance sur la porte 1 s du proprietaire du duel
        // (aDuelState[2] == 1, soit PlayerA), pas sur le tick logique. Server/ts25zone/S07_MyGame04.cpp:378,665
        var gateOpens = playerA?.OneSecondGateOpenCount ?? playerB?.OneSecondGateOpenCount ?? 0;
        if (gateOpens <= 0)
            return;

        if (playerA is null || playerB is null)
        {
            duels.TryEndActiveDuel(duel.PlayerA, out _);
            if (playerA is not null)
                zone.EndActiveDuel(playerA, DuelEndReason.OpponentNotFound);
            if (playerB is not null)
                zone.EndActiveDuel(playerB, DuelEndReason.OpponentNotFound);
            return;
        }

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

        duel.RemainingTicks -= gateOpens;
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
